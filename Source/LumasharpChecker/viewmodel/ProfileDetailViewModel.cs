using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static ProfileManager;

namespace LumasharpChecker.viewmodel
{
    partial class ProfileDetailViewModel : ObservableObject
    {
        public ProfileDetailViewModel(ProfileData profile, List<string> imagesFiles)
        {
            //profile null, imagesFiles 有 = 建新profile

            //profile 有, imagesFiles 有 = 編輯現有profile or 建新profile

            //profile 有, imagesFiles null = 編輯現有profile
        }
    }
}
