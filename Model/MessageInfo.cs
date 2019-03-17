using System.Collections.Generic;

namespace GenericRepository.Model
{

    public class MessageInfo
    {
        public List<string> Info { get; set; }
        public List<string> Warning { get; set; }
        public List<string> Error { get; set; }
        public List<string> Success { get; set; }

        public MessageInfo()
        {
            Info = new List<string>();
            Warning = new List<string>();
            Error = new List<string>();
            Success = new List<string>();
        }
        
        public MessageInfo(List<string> infos, List<string> warnings, List<string> errors, List<string> success)
        {
            Info = infos;
            Warning = warnings;
            Error = errors;
            Success = success;
        }

        public void Merge(MessageInfo other)
        {
            this.Info.AddRange(other.Info);
            this.Warning.AddRange(other.Warning);
            this.Error.AddRange(other.Error);
            this.Success.AddRange(other.Success);
        }
    }
}
