using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using commons.services.Saga;

namespace commons.services.Sagas
{
    public class SagaStep
    {
        public delegate Task StepCallback(SagaData form);
        public bool Local { get; set; }
        public StepCallback Action { get; set; }
        public StepCallback Compensation { get; set; }

        // saga step的唯一key，如果不提供用saga中steps中的索引表示
        public virtual string Key()
        {
            return null;
        }

    }
    public class SagaDefinitionBuilder<FormType> where FormType: class, SagaData
    {
        private SagaDefinition building;
        private SagaStep buildingStep;

        public SagaDefinitionBuilder()
        {
            building = new SagaDefinition();
        }

        public SagaDefinition Build(ISimpleSaga saga)
        {
            building.Saga = saga;
            return building;
        }

        public SagaDefinitionBuilder<FormType> Step()
        {
            var sagaStep = new SagaStep();
            buildingStep = sagaStep;
            building.AddStep(sagaStep);
            return this;
        }

        public delegate Task StepCallback(FormType form);

        private SagaStep.StepCallback ToStepCallback(StepCallback action)
        {
            if(action == null)
            {
                return null;
            }
            return (SagaData sd) =>
            {
                return action(sd as FormType);
            };
        }

        public SagaDefinitionBuilder<FormType> SetLocalAction(StepCallback action)
        {
            buildingStep.Local = true;
            buildingStep.Action = ToStepCallback(action);
            return this;
        }

        public SagaDefinitionBuilder<FormType> SetRemoteAction(StepCallback action)
        {
            buildingStep.Local = false;
            buildingStep.Action = ToStepCallback(action);

            var compensableAttrs = action.Method.GetCustomAttributes(typeof(Compensable), true);
            if(compensableAttrs.Count()>0)
            {
                var compensableAttr = compensableAttrs.First();
                var compensableAttrObj = compensableAttr as Compensable;
                var compensableActionName = compensableAttrObj.ActionName;
                var compensableMethod = action.Method.DeclaringType.GetMethod(compensableActionName);
                if(compensableMethod==null)
                {
                    throw new SagaAbortException($"can't find compensable method {compensableMethod} in class {action.Method.DeclaringType.FullName}");
                }
                var actionInvoker = action.Target;
                SagaStep.StepCallback compensableStepCallback = (SagaData form) =>
                {
                    object res = compensableMethod.Invoke(actionInvoker, new object[] { form });
                    return res as Task;
                };
                buildingStep.Compensation = compensableStepCallback;
            }
            return this;
        }

        public SagaDefinitionBuilder<FormType> WithCompensation(StepCallback compensation)
        {
            buildingStep.Compensation = ToStepCallback(compensation);
            return this;
        }
    }
    public class SagaDefinition
    {
        public ISimpleSaga Saga { get; set; }
        private List<SagaStep> _steps { get; set; }

        public SagaDefinition()
        {
            _steps = new List<SagaStep>();
        }

        public void AddStep(SagaStep step)
        {
            _steps.Add(step);
        }

        public IEnumerable<SagaStep> Steps
        {
            get
            {
                return _steps;
            }
        }



        public string KeyOfStep(SagaStep step)
        {
            var key = step.Key();
            if (key != null)
            {
                return key;
            }
            var i = 0;
            foreach(var s in Steps)
            {
                if(s == step)
                {
                    return i.ToString();
                }
                i++;
            }
            return step.GetType().FullName;
        }
    }
}
