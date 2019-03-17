using System;
using System.Collections.Generic;

namespace ManagementPortal.Model
{
    

    public class LeftNavigation
    {
        public string Text { get; set; }
        public int Level { get; set; }
        public string Href { get; set; }

        public string IconClass { get; set; }

        public List<LeftNavigation> Children;

        public LeftNavigation(int parentLevel)
        {
            Level = parentLevel + 1;
            Children = new List<LeftNavigation>();
        }
    }
}
