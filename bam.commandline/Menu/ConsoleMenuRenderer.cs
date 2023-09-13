using Bam.Sys;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Bam.Commandline.Menu
{
    public class ConsoleMenuRenderer : IMenuRenderer
    {
        public ConsoleMenuRenderer() { }

        protected ConsoleInputParser ConsoleInputParser
        {
            get;
            private set;
        }

        public void RenderMenu(IMenu menu)
        {
            throw new NotImplementedException();
        }

        public void RenderMenuFooter(IMenu menu)
        {
            throw new NotImplementedException();
        }

        public void RenderMenuHeader(IMenu menu)
        {
            throw new NotImplementedException();
        }
    }
}
