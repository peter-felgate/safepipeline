using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace SafePipeline
{
//    [DebuggerStepThrough]
    public interface IOperable
    {
    }

    public abstract class Operable<T> : IOperable
    {
        public abstract T Value { get; }
        public PipelineInfo Info { get; protected set; }
        protected object InputFromFailedStep;
        
        public object InputIntoFailedStep() => InputFromFailedStep;

        public TInputType InputIntoFailedStep<TInputType>()
        {
            if (!(this is Fail<T>)) return default;

            return InputFromFailedStep switch
            {
                Operable<TInputType> operable => operable.Value,
                TInputType input => input,
                _ => default
            };
        } 
        public static implicit operator T(Operable<T> item) => item.Value;
        public static implicit operator Exception(Operable<T> item) => (item as Fail<T>)?.Exception;
        public bool IsOk => !typeof(Fail<T>).IsAssignableFrom(GetType());
    }

    public class PipelineInfo
    {
        public string Info { get; set; }
    }

    public class Ok<T> : Operable<T>
    {
        public override T Value { get; }

        [DebuggerStepThrough]
        public Ok(T value)
        {
            Value = value;
        }
    }

    public class Skip<T> : Operable<T>
    {
        public override T Value => default;

        [DebuggerStepThrough]
        public Skip(PipelineInfo info)
        {
            Info = info;
        }

        [DebuggerStepThrough]
        public Skip(string info = "Skipping subsequent actions")
        {
            Info = new PipelineInfo { Info = info };
        }
    }

    public class Fail<T> : Operable<T>
    {
        public override T Value => default;

        public Exception Exception { get; }

        public Fail(object inputFromFailedStep, Exception exception = null)
        {
            InputFromFailedStep = inputFromFailedStep;
            Exception = exception;
        }
    }

    public static class SafePipeline
    {
        public static Task<T> AsTask<T>(this T input) => Task.FromResult(input);

        [DebuggerStepThrough]
        public static Task<Operable<T>> StartWith<T>(T item) => new Ok<T>(item).AsTask<Operable<T>>();

        public static async Task<Operable<TToType>> Then<TFromType, TToType>(this Task<Operable<TFromType>> inputTask,
            Func<TFromType, TToType> actor)
        {
            var input = await inputTask;

            switch (input)
            {
                case Ok<TFromType> ok when !ok.Value.Equals(default(TFromType)):
                    try
                    {
                        return new Ok<TToType>(actor(input)); // implicit conversion helps here
                    }
                    catch (Exception e)
                    {
                        return new Fail<TToType>(ok.Value, e);
                    }
                case Skip<TToType> skip:
                    return skip;
                case Fail<TFromType> fail:
                    return new Fail<TToType>(fail.InputIntoFailedStep(), fail.Exception);
            }

            return new Fail<TToType>(input.Value);
        }

        public static async Task<Operable<TToType>> Then<TFromType, TToType>(this Task<Operable<TFromType>> inputTask,
            Func<TFromType, Task<TToType>> actor)
        {
            var input = await inputTask;

            switch (input)
            {
                case Ok<TFromType> ok when !ok.Value.Equals(default(TFromType)):
                    try
                    {
                        var acted = await actor(input);

                        return new Ok<TToType>(acted); // implicit conversion helps here
                    }
                    catch (Exception e)
                    {
                        return new Fail<TToType>(input.Value, e);
                    }
                case Skip<TToType> skip:
                    return skip;
                case Fail<TFromType> fail:
                    return new Fail<TToType>(fail.InputIntoFailedStep(), fail.Exception);
            }

            return new Fail<TToType>(input.Value);
        }

        public static async Task<Operable<TToType>> Then<TFromType, TToType>(this Task<Operable<TFromType>> inputTask,
            Func<Operable<TFromType>, Operable<TToType>> actor)
        {
            var input = await inputTask;

            switch (input)
            {
                case Ok<TFromType> ok when !ok.Value.Equals(default(TFromType)):
                    try
                    {
                        return actor(input);
                    }
                    catch (Exception e)
                    {
                        return new Fail<TToType>(input.Value, e);
                    }
                case Skip<TToType> skip:
                    return skip;
                case Fail<TFromType> fail:
                    return new Fail<TToType>(fail.InputIntoFailedStep(), fail.Exception);
            }

            return new Fail<TToType>(input.Value);
        }

        public static Operable<TToType> AsOperable<TToType>(this Task<Operable<TToType>> input)
        {
            Task.WaitAll(input);

            return input.Result;
        }

        public static async Task<Operable<T>> OnSuccess<T>(this Task<Operable<T>> input, Action<Operable<T>> onSuccess)
        {
            var result = await input;

            if (result.IsOk && !(result is Skip<T>)) onSuccess(result);

            return result;
        }

        public static async Task<Operable<T>> OnFailure<T>(this Task<Operable<T>> input, Action<Operable<T>> onFailure)
        {
            var result = await input;

            if (!result.IsOk) onFailure(result);

            return result;
        }

        public static async Task<Operable<T>> OnSkip<T>(this Task<Operable<T>> input, Action<Operable<T>> onSkip)
        {
            var result = await input;

            if (result is Skip<T>) onSkip(result);

            return result;
        }

        public static async Task<Operable<T>> Do<T>(this Task<Operable<T>> input, Action<Operable<T>> doit)
        {
            var result = await input;

            doit(result);

            return result;
        }
    }

}
