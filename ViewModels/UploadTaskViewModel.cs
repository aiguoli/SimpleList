using CommunityToolkit.Mvvm.ComponentModel;

namespace SimpleList.ViewModels
{
    public class UploadTaskViewModel : ObservableObject
    {
        private string _name;
        private string _path;
        private string _status;
        private double _progress;
        private bool _isCompleted;
        
        public void StartUpload()
        {
            // TODO: Implement upload logic
        }
    }
}
