using System;
using System.Collections.Generic;
using System.Text;

namespace commons.services.Saga
{
    [AttributeUsage(AttributeTargets.Method)]
    public class Compensable : Attribute
    {
        private string _actionName;

        public Compensable(string actionName)
        {
            this._actionName = actionName;
        }

        public string ActionName
        {
            get
            {
                return _actionName;
            }
        }
    }
}
