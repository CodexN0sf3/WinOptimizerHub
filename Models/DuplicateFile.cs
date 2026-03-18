using System;
using WinOptimizerHub.Helpers;

namespace WinOptimizerHub.Models
{
    public class DuplicateFile : ObservableObject
    {
        public string Hash { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string FullPath { get; set; } = string.Empty;
        public long Size { get; set; }
        public DateTime LastModified { get; set; }

        private bool _isMarkedForDeletion;
        public bool IsMarkedForDeletion
        {
            get => _isMarkedForDeletion;
            set => SetProperty(ref _isMarkedForDeletion, value);
        }

        public string SizeDisplay => FormatHelper.FormatSize(Size);
    }
}