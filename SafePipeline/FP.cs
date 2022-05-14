using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace SafePipeline
{
    [DebuggerStepThrough]
    public abstract class Operable<T>
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

        protected object _input;
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
        public override T Value { get; }

        [DebuggerStepThrough]
        public Skip(PipelineInfo info)
        {
            Info = info;
        }

        [DebuggerStepThrough]
        public Skip(T value)
        {
            Value = value;
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
        /// <summary>
        /// when the last step in your pipeline is not async, add this as the last step to allow you
        /// to "await" the pipeline
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="input"></param>
        /// <returns></returns>
        public static Task<T> AsTask<T>(this T input) => Task.FromResult(input);

        /// <summary>
        /// this is the method to call to start the pipeline
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="item"></param>
        /// <returns></returns>
        [DebuggerStepThrough]
        public static Task<Operable<T>> StartWith<T>(T item) => new Ok<T>(item).AsTask<Operable<T>>();

        /// <summary>
        /// when the input step has finished, continue updating the context with the function provided
        /// </summary>
        /// <typeparam name="TFromType"></typeparam>
        /// <typeparam name="TToType"></typeparam>
        /// <param name="inputTask"></param>
        /// <param name="actor"></param>
        /// <returns></returns>
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
                    return FailWith<TToType>(fail.InputIntoFailedStep(), fail.Exception);
            }

            return new Fail<TToType>(input.Value);
        }

        public static async Task<Operable<TToType>> Then<TFromType, TToType>(
            this Task<Operable<TFromType>> inputTask,
            Func<TFromType, Task<Operable<TToType>>> actor)
        {
            Operable<TFromType> input = await inputTask;
            Operable<TFromType> operable = input;
            if (!(operable is Ok<TFromType> ok) || ok.Value.Equals((object)default(TFromType)))
                return operable is Skip<TToType> skip ? (Operable<TToType>)skip : (operable is Fail<TFromType> fail ? (Operable<TToType>)new Fail<TToType>(fail.InputIntoFailedStep(), fail.Exception) : (Operable<TToType>)new Fail<TToType>((object)input.Value));
            try
            {
                return await actor(input);
            }
            catch (Exception ex)
            {
                return (Operable<TToType>)new Fail<TToType>((object)input.Value, ex);
            }
        }

        /// <summary>
        /// when the input step has finished, continue updating the context with the function provided
        /// but return the operable value provided by the actor function.
        /// this allows you do stop further pipeline execution without ending in an error state
        /// </summary>
        /// <typeparam name="TFromType"></typeparam>
        /// <typeparam name="TToType"></typeparam>
        /// <param name="inputTask"></param>
        /// <param name="actor"></param>
        /// <returns></returns>
        public static async Task<Operable<TToType>> Check<TFromType, TToType>(this Task<Operable<TFromType>> inputTask,
            Func<TFromType, Task<Operable<TToType>>> actor)
        {
            var input = await inputTask;

            switch (input)
            {
                case Ok<TFromType> ok when !ok.Value.Equals(default(TFromType)):
                    try
                    {
                        var r = await actor(input);
                        return r; // implicit conversion helps here
                    }
                    catch (Exception e)
                    {
                        return new Fail<TToType>(ok.Value, e);
                    }
                case Skip<TToType> skip:
                    return skip;
                case Fail<TFromType> fail:
                    return FailWith<TToType>(fail.InputIntoFailedStep(), fail.Exception);
            }

            return new Fail<TToType>(input.Value);
        }

        /// <summary>
        /// used to pass the context to the end, even when in fail state
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="input"></param>
        /// <param name="ex"></param>
        /// <returns></returns>
        private static Fail<T> FailWith<T>(object input, Exception ex)
        {
            if (input is Fail<T> fail) return fail;

            return new Fail<T>(input, ex);
        }

        /// <summary>
        /// when the input step has finished, continue updating the context with the function provided
        /// </summary>
        /// <typeparam name="TFromType"></typeparam>
        /// <typeparam name="TToType"></typeparam>
        /// <param name="inputTask"></param>
        /// <param name="actor"></param>
        /// <returns></returns>
        public static async Task<Operable<TToType>> Then<TFromType, TToType>(this Task<Operable<TFromType>> inputTask,
            Func<TFromType, Task<TToType>> actor)
        {
            var input = await inputTask;

            switch (input)
            {
                case Ok<TFromType> ok when ok.Value?.Equals(default(TFromType)) == false:
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
                    return FailWith<TToType>(fail.InputIntoFailedStep(), fail.Exception);
            }

            return new Fail<TToType>(input.Value);
        }

        /// <summary>
        /// when the input step has finished, continue updating the context with the function provided
        /// </summary>
        /// <typeparam name="TFromType"></typeparam>
        /// <typeparam name="TToType"></typeparam>
        /// <param name="inputTask"></param>
        /// <param name="actor"></param>
        /// <returns></returns>
        public static async Task<Operable<TToType>> Then<TFromType, TToType>(this Task<Operable<TFromType>> inputTask,
            Func<TFromType, Operable<TToType>> actor)
        {
            var input = await inputTask;

            switch (input)
            {
                case Ok<TFromType> ok when ok.Value?.Equals(default(TFromType)) == false:
                    try
                    {
                        return actor(input.Value);
                    }
                    catch (Exception e)
                    {
                        return new Fail<TToType>(input.Value, e);
                    }
                case Skip<TToType> skip:
                    return skip;
                case Fail<TFromType> fail:
                    return FailWith<TToType>(fail.InputIntoFailedStep(), fail.Exception);
            }

            return new Fail<TToType>(input.Value);
        }

        /// <summary>
        /// if you are starting a pipeline in a place where the use of "await" is not possible
        /// you can add this to the end of the pipeline to get the actual value once all the
        /// steps in the pipeline have completed
        /// </summary>
        /// <typeparam name="TToType"></typeparam>
        /// <param name="input"></param>
        /// <returns></returns>
        public static Operable<TToType> AsOperable<TToType>(this Task<Operable<TToType>> input)
        {
            Task.WaitAll(input);

            return input.Result;
        }

        /// <summary>
        /// execute the function provided whenever the pipeline ends in a success state
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="input"></param>
        /// <param name="onSuccess"></param>
        /// <returns></returns>
        public static async Task<Operable<T>> OnSuccess<T>(this Task<Operable<T>> input, Action<Operable<T>> onSuccess)
        {
            var result = await input;

            if (result.IsOk && !(result is Skip<T>)) onSuccess(result);

            return result;
        }

        /// <summary>
        /// Execute the function provided whenever the pipeline ends in failure
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="input"></param>
        /// <param name="onFailure"></param>
        /// <returns></returns>
        public static async Task<Operable<T>> OnFailure<T>(this Task<Operable<T>> input, Action<Operable<T>> onFailure)
        {
            var result = await input;

            if (!result.IsOk) onFailure(result);

            return result;
        }

        /// <summary>
        /// Execute the function provided whenever the pipeline ends in a "Skip" state
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="input"></param>
        /// <param name="onSkip"></param>
        /// <returns></returns>
        public static async Task<Operable<T>> OnSkip<T>(this Task<Operable<T>> input, Action<Operable<T>> onSkip)
        {
            var result = await input;

            if (result is Skip<T>) onSkip(result);

            return result;
        }

        /// <summary>
        /// perform an action at any stage of the pipeline, the action should not affect the context
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="input"></param>
        /// <param name="doit"></param>
        /// <returns></returns>
        public static async Task<Operable<T>> Do<T>(this Task<Operable<T>> input, Action<Operable<T>> doit)
        {
            var result = await input;

            doit(result);

            return result;
        }

        /// <summary>
        /// in some cases you want an exception to be thrown after you have dealt
        /// with the failure cleanly in your own code
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="input"></param>
        /// <returns></returns>
        public static async Task<Operable<T>> ThrowOnException<T>(this Task<Operable<T>> input)
        {
            var result = await input;

            if (result.IsOk) return result;

            Exception ex = result;
            throw ex;
        }
    }

}
