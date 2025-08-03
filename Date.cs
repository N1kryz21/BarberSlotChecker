using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Net.Mime.MediaTypeNames;

namespace Cheacker
{
    internal class Date
    {
        protected string month { get; set; }
        protected string day { get; set; }
        protected DateTime date { get; set; }

        public Date()
        {

        }
        public Date(string month, string day,DateTime time)
        {
            this.month = month;
            this.day = day;
            this.date = time;

        }

        public override string ToString()
        {
            return string.Format("Date: {0}, Day: {1}, Time: {2:yyyy-MM-dd HH:mm}", month, day, date);
        }

    }
   
       
}
