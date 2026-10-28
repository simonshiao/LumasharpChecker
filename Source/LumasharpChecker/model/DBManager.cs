using LumasharpChecker.model;
using LiteDB;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace LumasharpChecker
{
    internal class DBManager
    {
        private readonly string dbPath = Path.Combine(AppContext.BaseDirectory, "MyData.db");
        private readonly string tableName = "SharpnessRecords";
        private static readonly SemaphoreSlim _dbSemaphore = new SemaphoreSlim(1, 1);

        public async Task AddSharpnessRecordAsync(string profileName, double sharpness, int dynamicThresholdCount)
        {
            var record = new SharpnessRecord
            {
                ProfileName = profileName,
                Sharpness = sharpness,
                Timestamp = DateTime.Now
            };

            await addSharpnessRecordAsync(record, dynamicThresholdCount);
        }

        public async Task addSharpnessRecordAsync(SharpnessRecord record, int dynamicThresholdCount)
        {
            await _dbSemaphore.WaitAsync();
            try
            {
                using (var db = new LiteDatabase(dbPath))
                {
                    var col = db.GetCollection<SharpnessRecord>(tableName);

                    // 插入新紀錄
                    col.Insert(record);
                    // 找出這個 profile 的所有記錄，按時間排序（新→舊）
                    var records = col.Find(x => x.ProfileName == record.ProfileName).OrderByDescending(x => x.Timestamp).ToList();
                    // 如果超過 dynamicThresholdCount ，就刪除最舊的那些
                    if (records.Count > dynamicThresholdCount)
                    {
                        var toDelete = records.Skip(dynamicThresholdCount).Select(r => r.Id).ToList();
                        col.DeleteMany(x => toDelete.Contains(x.Id));
                    }
                }
            }
            finally
            {
                _dbSemaphore.Release();
            }
        }

        public async Task<List<SharpnessRecord>> GetSharpnessRecordsAsync(string profileName)
        {
            await _dbSemaphore.WaitAsync();
            try
            {
                using (var db = new LiteDatabase(dbPath))
                {
                    var col = db.GetCollection<SharpnessRecord>(tableName);
                    return col.Find(x => x.ProfileName == profileName)
                              .OrderByDescending(x => x.Timestamp)
                              .ToList();
                }
            }
            finally
            {
                _dbSemaphore.Release();
            }
        }

        // 保留同步方法以向後相容，但內部使用非同步方法
        public void AddSharpnessRecord(string profileName, double sharpness, int dynamicThresholdCount)
        {
            AddSharpnessRecordAsync(profileName, sharpness, dynamicThresholdCount).GetAwaiter().GetResult();
        }

        public void addSharpnessRecord(SharpnessRecord record, int dynamicThresholdCount)
        {
            addSharpnessRecordAsync(record, dynamicThresholdCount).GetAwaiter().GetResult();
        }

        public List<SharpnessRecord> GetSharpnessRecords(string profileName)
        {
            return GetSharpnessRecordsAsync(profileName).GetAwaiter().GetResult();
        }
    }
}
