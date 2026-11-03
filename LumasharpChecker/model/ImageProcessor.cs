using System;
using System.Buffers.Text;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Cyntec.Logger;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using Emgu.CV.Util;
using static LumasharpChecker.model.Listener;
using Point = System.Drawing.Point;

namespace LumasharpChecker.model
{
    public class ImageProcessor
    {
        private const double TemplateSearchExpandRatio = 0.15;
        private const long CoarseTemplateMatchPixelThreshold = 1_000_000;
        private const int CoarseTemplateMatchScale = 8;
        private const int TemplateMatchRefineRadius = 6;
        private const int CoarseTemplateMatchCandidateCount = 3;
        private readonly string _profileBasePath;
        private bool _useTemplateMatching = true;
        //private Mat _template;
        public Rectangle? patternmatchroi;
        public Rectangle? monitorroi;
        public Rectangle? brightnessroi;
        public Rectangle? sharpnessroi;
        private double? _profileBrightnessMin;
        private double? _profileBrightnessMax;
        private double? _profileSharpnessMin;
        private string? _loadedProfileName;
        private ProfileManager? _loadedProfileManager;
        private Mat? _templateImage;
        private Mat? _coarseTemplateImage;
        private Mat? _halfTemplateImage;
        private MCvScalar _templateMean;
        private double _templateVariance;
        private Rectangle _templatePatternRoi;
        byte[]? imageBytes;

        public ImageProcessor(string profileBasePath = "profile")
        { 
            _profileBasePath = profileBasePath;
        }

        public ProcessResult Process(RequestJsonModel request)
        {
            imageBytes = Convert.FromBase64String(request.image_data);
            Mat mat = new Mat();
            CvInvoke.Imdecode(imageBytes, ImreadModes.Color, mat);

            return ProcessImage2(mat, request.profile);
        }



        internal ProcessResult ProcessImage(Mat image, string profile = "")
        {

            var _logger = Logmanager.loger;
            if (image == null || image.IsEmpty)
                throw new ArgumentException("無效的圖像輸入");

            try
            {
                _logger.Trace($"開始處理圖像，圖像大小: {image.Size}, profile: {profile}");
                ProfileManager profileManager = new("profile");
                if (profile != null)
                {
                    profileManager = new ProfileManager("profile");
                    if (profileManager.LoadProfile(profile))
                    {
                        _profileBrightnessMin = profileManager.BrightnessMin;
                        _profileBrightnessMax = profileManager.BrightnessMax;
                        _profileSharpnessMin = profileManager.SharpnessMin;
                    }
                    else
                    {
                        _profileBrightnessMin = 0;
                        _profileBrightnessMax = 0;
                        _profileSharpnessMin = 0;
                    }
                }

                // ROI 處理邏輯
                bool isMatched = false;
                Rectangle patternMatchRoiRect;

                if (_useTemplateMatching)
                {
                    //using (Mat roi = roiTemplate.SubMat(roiRect))
                    {
                    //    roi.CopyTo(templateImage); // 显式复制数据
                    }

                    patternMatchRoiRect = new Rectangle(profileManager.RoiX.GetValueOrDefault(), profileManager.RoiY.GetValueOrDefault(), profileManager.RoiW.GetValueOrDefault(), profileManager.RoiH.GetValueOrDefault());
                    //Mat roiTemplate = CvInvoke.Imread(profileManager.ImagePath);
                    Mat templateImage = new Mat(CvInvoke.Imread(profileManager.ImagePath), patternMatchRoiRect);
                    if (templateImage != null && !templateImage.IsEmpty)
                    {
                        //_logger.Trace($"使用模板匹配模式..., profile: {profile}");
                        var matchRoi = FindTemplateMatch(image, templateImage, patternMatchRoiRect);
                        if (matchRoi.HasValue)
                        {
                            patternMatchRoiRect = matchRoi.Value;
                            isMatched = true;
                            _logger.Trace($"模板匹配成功: {patternMatchRoiRect}, profile: {profile}");
                        }
                        else
                        {
                            _logger.Warn("模板匹配失敗，使用固定 ROI, profile: {profile}");
                            patternMatchRoiRect = patternmatchroi ?? new Rectangle(0, 0, image.Width, image.Height);
                        }
                    }
                    else
                    {
                        _logger.Warn("模板匹配已啟用但沒有模板，使用固定 ROI, profile: {profile}");
                        patternMatchRoiRect = patternmatchroi ?? new Rectangle(0, 0, image.Width, image.Height);
                    }
                }
                else
                {
                    _logger.Trace("使用固定 ROI 模式..., profile: {profile}");
                    patternMatchRoiRect = patternmatchroi ?? new Rectangle(0, 0, image.Width, image.Height);
                    _logger.Trace($"使用固定 ROI: {patternMatchRoiRect}");
                }

                // 驗證 ROI 範圍
                patternMatchRoiRect.Intersect(new Rectangle(0, 0, image.Width, image.Height));

                // 提取 ROI 區域
                using (Mat roiImage = new Mat(image, patternMatchRoiRect))
                {
                    if (roiImage.IsEmpty)
                        throw new ArgumentException($"ROI 區域大小為 0 , profile: {profile}");

                    // 計算亮度
                    Mat hsv = new Mat();    
                    CvInvoke.CvtColor(roiImage, hsv, ColorConversion.Bgr2Hsv);
                    MCvScalar brightnessScalar = CvInvoke.Mean(hsv);
                    double brightness = brightnessScalar.V2;  // V通道為亮度
                    _logger.Trace($"計算得到亮度值: {brightness:F2}, profile: {profile}");

                    // 計算銳利度
                    Mat gray = new Mat();
                    CvInvoke.CvtColor(roiImage, gray, ColorConversion.Bgr2Gray);

                    Mat sobelX = new Mat();
                    Mat sobelY = new Mat();
                    CvInvoke.Sobel(gray, sobelX, DepthType.Cv64F, 1, 0);
                    CvInvoke.Sobel(gray, sobelY, DepthType.Cv64F, 0, 1);

                    using (Matrix<double> gradient = new Matrix<double>(sobelX.Rows, sobelX.Cols))
                    {
                        // 使用 CartToPolar 计算幅值和方向
                        Mat magnitude = new Mat();
                        Mat direction = new Mat();
                        CvInvoke.CartToPolar(sobelX, sobelY, magnitude, direction);
                        MCvScalar mean = new MCvScalar();
                        MCvScalar stdDev = new MCvScalar();
                        CvInvoke.MeanStdDev(magnitude, ref mean, ref stdDev);
                        double sharpness = stdDev.V0 * stdDev.V0;  // 方差計算
                        _logger.Trace($"計算得到銳利度值: {sharpness:F2}, profile: {profile}");

                        return new ProcessResult
                        {
                            Brightness = brightness,
                            Sharpness = sharpness,
                            BrightnessSafe = CheckBrightnessSafety(brightness),
                            BrightnessMin = _profileBrightnessMin,
                            BrightnessMax = _profileBrightnessMax,
                            SharpnessSafe = CheckSharpnessSafety(sharpness),
                            SharpnessMin = _profileSharpnessMin,
                            //RoiImage = roiImage.Clone(),
                            RoiCoords = patternMatchRoiRect,
                            IsMatched = isMatched
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"圖像處理錯誤: {ex.Message}, profile: {profile}");
                throw;
            }
        }
        //樣板匹配跟監控拆開
        internal ProcessResult ProcessImage2(Mat image, string profile = "", bool includeSharpness = true)
        {
            var dbManager = new DBManager();
            var _logger = Logmanager.loger;
            if (image == null || image.IsEmpty)
                throw new ArgumentException("無效的圖像輸入");

            try
            {
                _logger.Trace($"開始處理圖像，圖像大小: {image.Size}, profile: {profile}");
                ProfileManager profileManager = GetLoadedProfile(profile, includeSharpness);
                if (profileManager == null)
                {
                    _logger.Warn($"Profile載入失敗，跳過處理該筆數據, profile: {profile}");
                    return new ProcessResult
                    {
                        Brightness = 0,
                        Sharpness = 0,
                        BrightnessSafe = false,
                        BrightnessMin = null,
                        BrightnessMax = null,
                        SharpnessSafe = !includeSharpness,
                        SharpnessMin = null,
                        RoiCoords = new Rectangle(0, 0, 0, 0),
                        BrightnessRoiCoords = new Rectangle(0, 0, 0, 0),
                        SharpnessRoiCoords = new Rectangle(0, 0, 0, 0),
                        IsMatched = false
                    };
                }

                // ROI 處理邏輯
                bool isMatched = false;
                Rectangle patternMatchRoiRect;
                Rectangle brightnessRoiRect = new Rectangle();
                Rectangle sharpnessRoiRect = new Rectangle();


                if (profileManager.TemplateMatching)
                {
                    patternMatchRoiRect = new Rectangle(profileManager.RoiX.GetValueOrDefault(), profileManager.RoiY.GetValueOrDefault(), profileManager.RoiW.GetValueOrDefault(), profileManager.RoiH.GetValueOrDefault());
                    Mat? templateImage = GetTemplateImage(profileManager, patternMatchRoiRect);
                    if (templateImage == null || templateImage.IsEmpty)
                    {
                        _logger.Warn($"模板匹配已啟用但沒有模板，profile: {profile}");
                        return CreateTemplateMatchFailure(includeSharpness);
                    }

                    var matchRoi = FindTemplateMatch(image, templateImage, patternMatchRoiRect, _coarseTemplateImage, _halfTemplateImage);
                    if (!matchRoi.HasValue)
                    {
                        _logger.Warn($"模板匹配失敗，profile: {profile}");
                        return CreateTemplateMatchFailure(includeSharpness);
                    }

                    patternMatchRoiRect = matchRoi.Value;
                    isMatched = true;
                    _logger.Trace($"模板匹配成功: {patternMatchRoiRect}, profile: {profile}");

                    brightnessRoiRect = OffsetRoiFromMatchedPattern(
                        patternMatchRoiRect,
                        profileManager.RoiX,
                        profileManager.RoiY,
                        profileManager.RoiX2,
                        profileManager.RoiY2,
                        profileManager.RoiW2,
                        profileManager.RoiH2);
                    if (includeSharpness)
                    {
                        sharpnessRoiRect = OffsetRoiFromMatchedPattern(
                            patternMatchRoiRect,
                            profileManager.RoiX,
                            profileManager.RoiY,
                            profileManager.RoiX3,
                            profileManager.RoiY3,
                            profileManager.RoiW3,
                            profileManager.RoiH3);
                    }
                }
                else
                {
                    brightnessRoiRect = new Rectangle(
                        profileManager.RoiX2.GetValueOrDefault(),
                        profileManager.RoiY2.GetValueOrDefault(),
                        profileManager.RoiW2.GetValueOrDefault(),
                        profileManager.RoiH2.GetValueOrDefault());
                    if (includeSharpness)
                    {
                        sharpnessRoiRect = new Rectangle(
                            profileManager.RoiX3.GetValueOrDefault(),
                            profileManager.RoiY3.GetValueOrDefault(),
                            profileManager.RoiW3.GetValueOrDefault(),
                            profileManager.RoiH3.GetValueOrDefault());
                    }

                    isMatched = true;
                }

                // 驗證 ROI 範圍
                Rectangle imageBounds = new Rectangle(0, 0, image.Width, image.Height);
                brightnessRoiRect.Intersect(imageBounds);
                if (includeSharpness)
                {
                    sharpnessRoiRect.Intersect(imageBounds);
                }

                if (brightnessRoiRect.Width <= 0 || brightnessRoiRect.Height <= 0)
                    throw new ArgumentException($"亮度 ROI 區域大小為 0 , profile: {profile}");
                if (includeSharpness && (sharpnessRoiRect.Width <= 0 || sharpnessRoiRect.Height <= 0))
                    throw new ArgumentException($"銳利度 ROI 區域大小為 0 , profile: {profile}");

                double brightness;
                double sharpness = 0;

                using (Mat brightnessRoiImage = new Mat(image, brightnessRoiRect))
                {
                    brightness = CalculateBrightness(brightnessRoiImage);
                    _logger.Trace($"計算得到亮度值: {brightness:F2}, profile: {profile}");
                }

                if (includeSharpness)
                {
                    using (Mat sharpnessRoiImage = new Mat(image, sharpnessRoiRect))
                    {
                        sharpness = CalculateSharpness(sharpnessRoiImage);
                        _logger.Trace($"計算得到銳利度值: {sharpness:F2}, profile: {profile}");
                    }
                }

                //將profile名稱跟銳利度存入mydata.db
                if (includeSharpness && profileManager.EnableDynamicThreshold)
                    dbManager.AddSharpnessRecord(profile, sharpness, profileManager.DynamicThresholdCount);

                return new ProcessResult
                {
                    Brightness = brightness,
                    Sharpness = sharpness,
                    BrightnessSafe = CheckBrightnessSafety(brightness),
                    BrightnessMin = _profileBrightnessMin,
                    BrightnessMax = _profileBrightnessMax,
                    SharpnessSafe = !includeSharpness || CheckSharpnessSafety(sharpness),
                    SharpnessMin = _profileSharpnessMin,
                    RoiCoords = brightnessRoiRect,
                    BrightnessRoiCoords = brightnessRoiRect,
                    SharpnessRoiCoords = sharpnessRoiRect,
                    IsMatched = isMatched
                };
            }
            catch (Exception ex)
            {
                _logger.Error($"圖像處理錯誤: {ex.Message}, profile: {profile}");
                throw;
            }
        }
        private bool CheckBrightnessSafety(double value)
        {
            return value >= _profileBrightnessMin && value <= _profileBrightnessMax;
        }

        private bool CheckSharpnessSafety(double value)
        {
            return value >= _profileSharpnessMin;
        }

        private ProcessResult CreateTemplateMatchFailure(bool includeSharpness)
        {
            return new ProcessResult
            {
                Brightness = 0,
                Sharpness = 0,
                BrightnessSafe = false,
                BrightnessMin = _profileBrightnessMin,
                BrightnessMax = _profileBrightnessMax,
                SharpnessSafe = !includeSharpness,
                SharpnessMin = _profileSharpnessMin,
                RoiCoords = Rectangle.Empty,
                BrightnessRoiCoords = Rectangle.Empty,
                SharpnessRoiCoords = Rectangle.Empty,
                IsMatched = false
            };
        }

        private ProfileManager? GetLoadedProfile(string profile, bool includeSharpness)
        {
            if (_loadedProfileManager != null && string.Equals(_loadedProfileName, profile, StringComparison.OrdinalIgnoreCase))
            {
                return _loadedProfileManager;
            }

            var profileManager = new ProfileManager(_profileBasePath);
            if (!profileManager.LoadProfile(profile))
            {
                return null;
            }

            _loadedProfileName = profile;
            _loadedProfileManager = profileManager;
            _profileBrightnessMin = profileManager.BrightnessMin;
            _profileBrightnessMax = profileManager.BrightnessMax;
            _profileSharpnessMin = profileManager.SharpnessMin;
            _templateImage?.Dispose();
            _coarseTemplateImage?.Dispose();
            _halfTemplateImage?.Dispose();
            _templateImage = null;
            _coarseTemplateImage = null;
            _halfTemplateImage = null;
            _templatePatternRoi = Rectangle.Empty;
            return profileManager;
        }

        private Mat? GetTemplateImage(ProfileManager profileManager, Rectangle patternMatchRoiRect)
        {
            if (_templateImage != null && !_templateImage.IsEmpty && _templatePatternRoi == patternMatchRoiRect)
            {
                return _templateImage;
            }

            if (string.IsNullOrWhiteSpace(profileManager.ImagePath))
            {
                return null;
            }

            _templateImage?.Dispose();
            _coarseTemplateImage?.Dispose();
            _halfTemplateImage?.Dispose();
            _templateImage = null;
            _coarseTemplateImage = null;
            _halfTemplateImage = null;
            using Mat patternImage = CvInvoke.Imread(profileManager.ImagePath);
            if (patternImage.IsEmpty)
            {
                return null;
            }

            Rectangle safeRoi = Rectangle.Intersect(patternMatchRoiRect, new Rectangle(0, 0, patternImage.Width, patternImage.Height));
            if (safeRoi.Width <= 0 || safeRoi.Height <= 0)
            {
                return null;
            }

            using Mat templateRoi = new Mat(patternImage, safeRoi);
            _templateImage = templateRoi.Clone();
            if ((long)_templateImage.Width * _templateImage.Height >= CoarseTemplateMatchPixelThreshold)
            {
                MCvScalar templateStd = new MCvScalar();
                _templateMean = new MCvScalar();
                CvInvoke.MeanStdDev(_templateImage, ref _templateMean, ref templateStd);
                _templateVariance = templateStd.V0 * templateStd.V0 + templateStd.V1 * templateStd.V1 + templateStd.V2 * templateStd.V2;
                var coarseSize = new System.Drawing.Size(
                    Math.Max(1, (int)Math.Round(_templateImage.Width / (double)CoarseTemplateMatchScale)),
                    Math.Max(1, (int)Math.Round(_templateImage.Height / (double)CoarseTemplateMatchScale)));
                _coarseTemplateImage = new Mat();
                CvInvoke.Resize(_templateImage, _coarseTemplateImage, coarseSize, 0, 0, Inter.Area);
                var halfSize = new System.Drawing.Size(
                    Math.Max(1, (int)Math.Round(_templateImage.Width / 2.0)),
                    Math.Max(1, (int)Math.Round(_templateImage.Height / 2.0)));
                _halfTemplateImage = new Mat();
                CvInvoke.Resize(_templateImage, _halfTemplateImage, halfSize, 0, 0, Inter.Area);
            }
            _templatePatternRoi = safeRoi;
            return _templateImage;
        }

        private static Rectangle OffsetRoiFromMatchedPattern(Rectangle matchedPattern, int? patternX, int? patternY, int? targetX, int? targetY, int? targetW, int? targetH)
        {
            var xOffset = patternX.GetValueOrDefault() - targetX.GetValueOrDefault();
            var yOffset = patternY.GetValueOrDefault() - targetY.GetValueOrDefault();
            return new Rectangle(
                matchedPattern.X - xOffset,
                matchedPattern.Y - yOffset,
                targetW.GetValueOrDefault(),
                targetH.GetValueOrDefault());
        }

        private static double CalculateBrightness(Mat roiImage)
        {
            using Mat hsv = new Mat();
            CvInvoke.CvtColor(roiImage, hsv, ColorConversion.Bgr2Hsv);
            MCvScalar brightnessScalar = CvInvoke.Mean(hsv);
            return brightnessScalar.V2;
        }

        private static double CalculateSharpness(Mat roiImage)
        {
            using Mat gray = new Mat();
            CvInvoke.CvtColor(roiImage, gray, ColorConversion.Bgr2Gray);

            using Mat sobelX = new Mat();
            using Mat sobelY = new Mat();
            CvInvoke.Sobel(gray, sobelX, DepthType.Cv64F, 1, 0);
            CvInvoke.Sobel(gray, sobelY, DepthType.Cv64F, 0, 1);

            using Mat magnitude = new Mat();
            using Mat direction = new Mat();
            CvInvoke.CartToPolar(sobelX, sobelY, magnitude, direction);
            MCvScalar mean = new MCvScalar();
            MCvScalar stdDev = new MCvScalar();
            CvInvoke.MeanStdDev(magnitude, ref mean, ref stdDev);
            return stdDev.V0 * stdDev.V0;
        }
        
        private Rectangle? FindTemplateMatch(Mat sourceImage, Mat patternImage, Rectangle patternRoi, Mat? coarsePatternImage = null, Mat? halfPatternImage = null)
        {
            // 擴展搜尋範圍
            int expandX = (int)(patternRoi.Width * TemplateSearchExpandRatio);
            int expandY = (int)(patternRoi.Height * TemplateSearchExpandRatio);

            Rectangle expandedRect = new Rectangle(
                patternRoi.X - expandX,
                patternRoi.Y - expandY,
                patternRoi.Width + expandX * 2,
                patternRoi.Height + expandY * 2
            );

            //補邊
            Rectangle imageBounds = new Rectangle(0, 0, sourceImage.Width, sourceImage.Height);
            Rectangle safeRegion = Rectangle.Intersect(expandedRect, imageBounds);
            using Mat croppedRegion = new Mat(sourceImage, safeRegion);
            using Mat paddedRegion = new Mat();

            int padTop = Math.Max(0, -expandedRect.Y);
            int padLeft = Math.Max(0, -expandedRect.X);
            int padBottom = Math.Max(0, expandedRect.Bottom - sourceImage.Height);
            int padRight = Math.Max(0, expandedRect.Right - sourceImage.Width);

            if (padTop > 0 || padLeft > 0 || padBottom > 0 || padRight > 0)
            {
                CvInvoke.CopyMakeBorder(croppedRegion, paddedRegion, padTop, padBottom, padLeft, padRight, BorderType.Constant, new MCvScalar(0, 0, 0));
                return FindTemplateMatchInRegion(paddedRegion, patternImage, coarsePatternImage, halfPatternImage, expandedRect.Location);
            }

            return FindTemplateMatchInRegion(croppedRegion, patternImage, coarsePatternImage, halfPatternImage, expandedRect.Location);
        }

        private Rectangle? FindTemplateMatchInRegion(Mat region, Mat pattern, Mat? coarsePattern, Mat? halfPattern, Point origin)
        {
            if (coarsePattern == null || halfPattern == null || _templateVariance <= 1e-12 || pattern.NumberOfChannels != 3 || region.NumberOfChannels != 3)
                return FindFullResolutionTemplateMatch(region, pattern, origin);

            if (TryFindFastTemplateMatch(region, pattern, coarsePattern, halfPattern, origin, _templateMean, _templateVariance, out Rectangle fastMatch))
                return fastMatch;

            // 接近門檻或候選分數相近時，使用原本完整搜尋區的全解析度比對。
            return FindFullResolutionTemplateMatch(region, pattern, origin);
        }

        private static bool TryFindFastTemplateMatch(
            Mat searchRegion, Mat pattern, Mat coarsePattern, Mat halfPattern,
            Point origin, MCvScalar templateMean, double templateVariance, out Rectangle match)
        {
            match = Rectangle.Empty;
            if (searchRegion.Width < pattern.Width || searchRegion.Height < pattern.Height)
                return false;

            var coarseSize = new System.Drawing.Size(
                Math.Max(coarsePattern.Width, (int)Math.Round(searchRegion.Width / (double)CoarseTemplateMatchScale)),
                Math.Max(coarsePattern.Height, (int)Math.Round(searchRegion.Height / (double)CoarseTemplateMatchScale)));
            using Mat coarseSearch = new Mat();
            CvInvoke.Resize(searchRegion, coarseSearch, coarseSize, 0, 0, Inter.Area);
            using Mat coarseScores = new Mat();
            CvInvoke.MatchTemplate(coarseSearch, coarsePattern, coarseScores, TemplateMatchingType.CcoeffNormed);

            var halfSize = new System.Drawing.Size(
                Math.Max(halfPattern.Width, (int)Math.Round(searchRegion.Width / 2.0)),
                Math.Max(halfPattern.Height, (int)Math.Round(searchRegion.Height / 2.0)));
            using Mat halfSearch = new Mat();
            CvInvoke.Resize(searchRegion, halfSearch, halfSize, 0, 0, Inter.Area);
            double coarseScaleX = (double)coarseSearch.Width / searchRegion.Width;
            double coarseScaleY = (double)coarseSearch.Height / searchRegion.Height;
            double halfScaleX = (double)halfSearch.Width / searchRegion.Width;
            double halfScaleY = (double)halfSearch.Height / searchRegion.Height;
            int suppressX = Math.Max(1, (int)Math.Ceiling(TemplateMatchRefineRadius * coarseScaleX));
            int suppressY = Math.Max(1, (int)Math.Ceiling(TemplateMatchRefineRadius * coarseScaleY));
            var groups = new List<(Rectangle Bounds, List<(int Index, Rectangle Bounds)> Candidates)>();

            for (int i = 0; i < CoarseTemplateMatchCandidateCount; i++)
            {
                double min = 0, max = 0;
                Point minLocation = Point.Empty, maxLocation = Point.Empty;
                CvInvoke.MinMaxLoc(coarseScores, ref min, ref max, ref minLocation, ref maxLocation);
                if (max < -1.5) break;

                int sourceX = (int)Math.Round(maxLocation.X / coarseScaleX);
                int sourceY = (int)Math.Round(maxLocation.Y / coarseScaleY);
                int halfX = (int)Math.Round(sourceX * halfScaleX);
                int halfY = (int)Math.Round(sourceY * halfScaleY);
                const int halfRadius = 5;
                Rectangle locations = Rectangle.Intersect(
                    new Rectangle(halfX - halfRadius, halfY - halfRadius, halfRadius * 2 + 1, halfRadius * 2 + 1),
                    new Rectangle(0, 0, halfSearch.Width - halfPattern.Width + 1, halfSearch.Height - halfPattern.Height + 1));
                if (locations.Width > 0 && locations.Height > 0)
                {
                    bool grouped = false;
                    for (int groupIndex = 0; groupIndex < groups.Count; groupIndex++)
                    {
                        var group = groups[groupIndex];
                        Rectangle merged = Rectangle.Union(group.Bounds, locations);
                        // 候選靠近時共用一次比對，仍分別從各自原有範圍取最高分。
                        if (merged.Width <= 48 && merged.Height <= 48)
                        {
                            group.Candidates.Add((i, locations));
                            groups[groupIndex] = (merged, group.Candidates);
                            grouped = true;
                            break;
                        }
                    }
                    if (!grouped)
                        groups.Add((locations, new List<(int Index, Rectangle Bounds)> { (i, locations) }));
                }

                Rectangle suppression = Rectangle.Intersect(
                    new Rectangle(maxLocation.X - suppressX, maxLocation.Y - suppressY, suppressX * 2 + 1, suppressY * 2 + 1),
                    new Rectangle(0, 0, coarseScores.Width, coarseScores.Height));
                CvInvoke.Rectangle(coarseScores, suppression, new MCvScalar(-2), -1);
            }

            var candidates = new List<(int Index, double Score, Point Location)>();
            foreach (var group in groups)
            {
                Rectangle searchBounds = new Rectangle(group.Bounds.X, group.Bounds.Y,
                    halfPattern.Width + group.Bounds.Width - 1, halfPattern.Height + group.Bounds.Height - 1);
                using Mat halfRegion = new Mat(halfSearch, searchBounds);
                using Mat halfScores = new Mat();
                CvInvoke.MatchTemplate(halfRegion, halfPattern, halfScores, TemplateMatchingType.CcoeffNormed);
                foreach (var candidate in group.Candidates)
                {
                    Rectangle scoreBounds = new Rectangle(candidate.Bounds.X - group.Bounds.X, candidate.Bounds.Y - group.Bounds.Y,
                        candidate.Bounds.Width, candidate.Bounds.Height);
                    using Mat candidateScores = new Mat(halfScores, scoreBounds);
                    double halfMin = 0, halfMax = 0;
                    Point halfMinLocation = Point.Empty, halfMaxLocation = Point.Empty;
                    CvInvoke.MinMaxLoc(candidateScores, ref halfMin, ref halfMax, ref halfMinLocation, ref halfMaxLocation);
                    candidates.Add((candidate.Index, halfMax, new Point(
                        (int)Math.Round((candidate.Bounds.X + halfMaxLocation.X) / halfScaleX),
                        (int)Math.Round((candidate.Bounds.Y + halfMaxLocation.Y) / halfScaleY))));
                }
            }

            if (candidates.Count == 0) return false;
            var bestHalf = candidates.OrderByDescending(candidate => candidate.Score).ThenBy(candidate => candidate.Index).First();
            // 相近分數但位置相距很遠時，保留原本的完整精比。
            if (candidates.Any(candidate => bestHalf.Score - candidate.Score < 0.01 &&
                    (Math.Abs(bestHalf.Location.X - candidate.Location.X) > 16 || Math.Abs(bestHalf.Location.Y - candidate.Location.Y) > 16)))
                return false;

            double pixelCount = (double)pattern.Width * pattern.Height;
            double bestScore = double.NegativeInfinity, secondScore = double.NegativeInfinity;
            Point bestLocation = Point.Empty;
            int maxX = searchRegion.Width - pattern.Width;
            int maxY = searchRegion.Height - pattern.Height;
            for (int y = Math.Max(0, bestHalf.Location.Y - 1); y <= Math.Min(maxY, bestHalf.Location.Y + 1); y++)
            {
                for (int x = Math.Max(0, bestHalf.Location.X - 1); x <= Math.Min(maxX, bestHalf.Location.X + 1); x++)
                {
                    using Mat candidate = new Mat(searchRegion, new Rectangle(x, y, pattern.Width, pattern.Height));
                    MCvScalar mean = new MCvScalar(), std = new MCvScalar();
                    CvInvoke.MeanStdDev(candidate, ref mean, ref std);
                    double variance = std.V0 * std.V0 + std.V1 * std.V1 + std.V2 * std.V2;
                    if (variance <= 1e-12) continue;
                    // CcoeffNormed 的逐通道去均值相關係數；只計算候選座標，避免多次重建大型比對矩陣。
                    double centeredDot = pattern.Dot(candidate) - pixelCount *
                        (templateMean.V0 * mean.V0 + templateMean.V1 * mean.V1 + templateMean.V2 * mean.V2);
                    double score = centeredDot / (pixelCount * Math.Sqrt(templateVariance * variance));
                    if (!double.IsFinite(score)) continue;
                    if (score > bestScore)
                    {
                        secondScore = bestScore;
                        bestScore = score;
                        bestLocation = new Point(x, y);
                    }
                    else if (score > secondScore)
                    {
                        secondScore = score;
                    }
                }
            }

            // 接近 0.5 門檻或兩個像素分數幾乎相同時，避免浮點誤差改變結果。
            if (bestScore < 0.501 || bestScore - secondScore < 0.00001)
                return false;

            match = new Rectangle(bestLocation.X + origin.X, bestLocation.Y + origin.Y, pattern.Width, pattern.Height);
            return true;
        }

        private static Rectangle? FindFullResolutionTemplateMatch(Mat searchRegion, Mat patternImage, Point searchOrigin)
        {
            using (Mat result = new Mat())
            {
                CvInvoke.MatchTemplate(searchRegion, patternImage, result, TemplateMatchingType.CcoeffNormed);

                double minVal = 0, maxVal = 0;
                Point minLoc = Point.Empty, maxLoc = Point.Empty;
                CvInvoke.MinMaxLoc(result, ref minVal, ref maxVal, ref minLoc, ref maxLoc);

                if (maxVal >= 0.5)
                {
                    Point matchLocInSource = new Point(maxLoc.X + searchOrigin.X, maxLoc.Y + searchOrigin.Y);
                    return new Rectangle(matchLocInSource, patternImage.Size);
                }
            }
            return null;
        }

        public class ProcessResult
        {
            public double Brightness { get; set; }
            public double Sharpness { get; set; }
            public bool BrightnessSafe { get; set; }
            public double? BrightnessMin { get; set; }
            public double? BrightnessMax { get; set; }
            public bool SharpnessSafe { get; set; }
            public double? SharpnessMin { get; set; }
            //public Mat RoiImage { get; set; }
            public Rectangle RoiCoords { get; set; }
            public Rectangle BrightnessRoiCoords { get; set; }
            public Rectangle SharpnessRoiCoords { get; set; }
            public bool IsMatched { get; set; }
        }

        private void loadProfile()
        {
            //注意有不同recipe但站別相同名字
        }

        //public class ProfileManager
        //{
        //    public double BrightnessMin { get; private set; }
        //    public double BrightnessMax { get; private set; }
        //    public double SharpnessMin { get; private set; }

        //    public ProfileManager()
        //    {
        //        // 實現配置加載邏輯
        //    }
        //    public ProfileManager(string profilePath)
        //    {
        //        // 實現配置加載邏輯
        //    }

        //    public void LoadProfile(string profile)
        //    {
        //        // 示例加載邏輯
        //        BrightnessMin = 50;
        //        BrightnessMax = 200;
        //        SharpnessMin = 30;
        //    }
        //}

        //python calculate_reference_value
        public async Task<AnalysisResult> CalculateReferenceValues(List<string> 照片路徑s)
        {
            try
            {
                // 前置檢查
                if (照片路徑s.Count == 0)
                {
                    MessageBox.Show("請先載入標準圖像", "警告", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return null;
                }

                //if (ROI == null && !UseTemplateMatching)
                //{
                //    MessageBox.Show("請先設定 ROI 或啟用模板匹配", "警告", MessageBoxButton.OK, MessageBoxImage.Warning);
                //    return;
                //}

                // 核心計算邏輯
                //var (brightnessValues, sharpnessValues) = ProcessImages(照片路徑s);

                var(brightnessValues, sharpnessValues) = await ProcessImagesAsync(照片路徑s);



                StatisticalCalculator statisticalCalculator = new();
                AnalysisResult analysisResult = new ();
                analysisResult.SharpnessValues = sharpnessValues;
                analysisResult.BrightnessValues = brightnessValues;
                var t = statisticalCalculator.CalculateStatisticalRanges(brightnessValues.ToArray(), sharpnessValues.ToArray());
                analysisResult.Brightness = t.Brightness;
                analysisResult.Sharpness = t.Sharpness;
                return analysisResult;
                //ValidateDataCount(brightnessValues.Count);

                //var ranges = CalculateStatisticalRanges(brightnessValues, sharpnessValues);
                //SaveProfile(ranges, ROI.Value);

                //ShowDistributionDialog(brightnessValues, sharpnessValues, ranges);
            }
            catch (Exception ex)
            {
                //UpdateLog($"計算錯誤: {ex.Message}");
                MessageBox.Show(ex.Message, "嚴重錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            return null;
        }

        private (List<double> brightness, List<double> sharpness) ProcessImages(List<string> 照片路徑s)
        {
            List<double> brightnessValues = new List<double>();
            List<double> sharpnessValues = new List<double>();

            var StandardImages = LoadImagesParallel(照片路徑s, out List<string> errors);
            Trace.WriteLine("");
            Mat m1 = StandardImages[0];
            Mat template = new(m1, patternmatchroi ?? new Rectangle());
            foreach (var img in StandardImages)
            {
                var UseTemplateMatching = true;
                try
                {
                    Rectangle roi = UseTemplateMatching ?
                        FindTemplateMatch(img, template, new Rectangle(patternmatchroi.Value.X, patternmatchroi.Value.Y, patternmatchroi.Value.Width, patternmatchroi.Value.Height)) ?? throw new InvalidOperationException("模板匹配失敗") : new Rectangle();//; ROI.Value;

                    using (Mat roiImg = new Mat(img, roi))
                    {
                        var metrics = CalculateImageMetrics(roiImg);
                        brightnessValues.Add(metrics.brightness);
                        sharpnessValues.Add(metrics.sharpness);
                    }
                }
                catch (Exception ex)
                {
                    //UpdateLog($"影像處理錯誤: {ex.Message}");
                }
            }
            return (brightnessValues, sharpnessValues);
        }



        private async Task<(List<double> brightness, List<double> sharpness)> ProcessImagesAsync(List<string> 照片路徑s)
        {
            // 並行載入所有圖片 (非同步IO操作)
            var (standardImages, errors) = await Task.Run(() =>
            {
                List<string> localErrors;
                var images = LoadImagesParallel(照片路徑s, out localErrors);
                return (images, localErrors);
            });

            // 初始化模板 (僅需處理一次)
            Mat template = await InitializeTemplateAsync(standardImages);

            // 並行處理所有圖片分析
            var results = await ProcessImagesInParallelAsync(standardImages, template);

            return (results.brightness, results.sharpness);
        }

        //------------------------------------------
        // 輔助方法
        //------------------------------------------
        private async Task<Mat> InitializeTemplateAsync(IReadOnlyList<Mat> standardImages)
        {
            //取第一張
            return await Task.Run(() =>
            {
                if (standardImages.Count == 0) return new Mat();
                Mat m1 = standardImages[0];
                return new Mat(m1, patternmatchroi ?? new Rectangle());
            });
        }

        private async Task<(List<double> brightness, List<double> sharpness)> ProcessImagesInParallelAsync(List<Mat> standardImages, Mat template)
        {
            var brightnessValues = new ConcurrentBag<double>();
            var sharpnessValues = new ConcurrentBag<double>();
            var UseTemplateMatching = true;
            await Task.Run(() =>
            {
                Parallel.ForEach(standardImages, new ParallelOptions
                {
                    MaxDegreeOfParallelism = Environment.ProcessorCount // 根據CPU核心數調整
                }, img =>
                {
                    try
                    {
                        var patternMatchedRoi = UseTemplateMatching
                            ? FindTemplateMatch(img, template, new Rectangle(patternmatchroi.Value.X, patternmatchroi.Value.Y, patternmatchroi.Value.Width, patternmatchroi.Value.Height)) ?? throw new InvalidOperationException("模板匹配失敗")
                            : new Rectangle();
                        Rectangle roi =new Rectangle();
                        {
                            //patternmatchroi monitorroi 的關係
                            var x = patternmatchroi.Value.X - monitorroi.Value.X;
                            var y = patternmatchroi.Value.Y - monitorroi.Value.Y;
                            roi = new Rectangle(patternMatchedRoi.X - x, patternMatchedRoi.Y - y, monitorroi.Value.Width, monitorroi.Value.Height);
                        }
                        var brightnessSourceRoi = brightnessroi ?? monitorroi ?? new Rectangle();
                        var sharpnessSourceRoi = sharpnessroi ?? monitorroi ?? new Rectangle();
                        var brightnessRoi = OffsetRoiFromMatchedPattern(patternMatchedRoi, patternmatchroi.Value.X, patternmatchroi.Value.Y, brightnessSourceRoi.X, brightnessSourceRoi.Y, brightnessSourceRoi.Width, brightnessSourceRoi.Height);
                        var sharpnessRoi = OffsetRoiFromMatchedPattern(patternMatchedRoi, patternmatchroi.Value.X, patternmatchroi.Value.Y, sharpnessSourceRoi.X, sharpnessSourceRoi.Y, sharpnessSourceRoi.Width, sharpnessSourceRoi.Height);
                        using (Mat brightnessRoiImg = new Mat(img, brightnessRoi))
                        using (Mat sharpnessRoiImg = new Mat(img, sharpnessRoi))
                        {
                            brightnessValues.Add(CalculateBrightness(brightnessRoiImg));
                            sharpnessValues.Add(CalculateSharpness(sharpnessRoiImg));
                        }
                    }
                    catch (Exception ex)
                    {
                        Trace.WriteLine($"影像處理錯誤: {ex.Message}");
                    }
                });
            });

            return (brightnessValues.ToList(), sharpnessValues.ToList());
        }

        private (double brightness, double sharpness) CalculateImageMetrics(Mat roiImg)
        {
            // Calculate brightness
            Mat hsv = new Mat();
            CvInvoke.CvtColor(roiImg, hsv, ColorConversion.Bgr2Hsv);
            VectorOfMat hsvChannels = new VectorOfMat();
            CvInvoke.Split(hsv, hsvChannels);

            var vvv = CvInvoke.Mean(hsvChannels[2]);
            double brightness = CvInvoke.Mean(hsvChannels[2]).V0;


            // Calculate sharpness
            Mat gray = new Mat();
            CvInvoke.CvtColor(roiImg, gray, ColorConversion.Bgr2Gray);

            Mat sobelX = new Mat();
            Mat sobelY = new Mat();
            CvInvoke.Sobel(gray, sobelX, DepthType.Cv64F, 1, 0, 3);
            CvInvoke.Sobel(gray, sobelY, DepthType.Cv64F, 0, 1, 3);

            Mat gradientSq = new Mat();
            CvInvoke.Multiply(sobelX, sobelX, gradientSq);
            using (Mat temp = new Mat())
            {
                CvInvoke.Multiply(sobelY, sobelY, temp);
                CvInvoke.Add(gradientSq, temp, gradientSq);
            }

            Mat gradient = new Mat();
            CvInvoke.Sqrt(gradientSq, gradient);

            // Calculate variance
            MCvScalar mean = CvInvoke.Mean(gradient);
            Mat sqrGradient = new Mat();
            CvInvoke.Pow(gradient, 2, sqrGradient);
            MCvScalar meanSqr = CvInvoke.Mean(sqrGradient);
            double sharpness = meanSqr.V0 - (mean.V0 * mean.V0);

            hsv.Dispose();
            gray.Dispose();
            sobelX.Dispose();
            sobelY.Dispose();
            gradientSq.Dispose();
            gradient.Dispose();
            sqrGradient.Dispose();

            return (brightness, sharpness);
        }
        public static List<Mat> LoadImagesParallel(
                                List<string> photoPaths,
                                out List<string> errors,
                                ImreadModes loadMode = ImreadModes.Color)
        {
            List<Mat> images = new List<Mat>();
            var _errors = new List<string>();
            object lockObj = new object();

            Parallel.ForEach(photoPaths, path =>
            {
                // 文件存在性检查
                if (!File.Exists(path))
                {
                    lock (lockObj) _errors.Add($"文件不存在: {path}");
                    return;
                }

                try
                {
                    Mat image = CvInvoke.Imread(path, loadMode);
                    if (image.IsEmpty)
                    {
                        lock (lockObj) _errors.Add($"无法解码图像: {path}");
                        return;
                    }

                    lock (lockObj) images.Add(image);
                }
                catch (Exception ex)
                {
                    lock (lockObj) _errors.Add($"加载失败 [{path}]: {ex.Message}");
                }
            });
            errors = _errors;
            return images;
        }
    }
}
