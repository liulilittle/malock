namespace malock
{
    using global::malock.Client;
    using global::malock.Core;
    using global::malock.NN;
    using System;
    using System.Collections.Generic;
    using System.Reflection;
    using Timeout = System.Threading.Timeout;

    public static class Malock
    {
        private static readonly TimerScheduler scheduler = new TimerScheduler(100);
        private static readonly Dictionary<string, NnsClient> nnss = new Dictionary<string, NnsClient>();
        private static readonly Dictionary<string, MalockClient> malocks = new Dictionary<string, MalockClient>();
        /// <summary>
        /// 连接中断后重连间隔时间
        /// </summary>
        public const int ReconnectionTime = 100;
        /// <summary>
        /// 连接中断后平滑到另一个连接的平滑时间
        /// </summary>
        public const int SmoothingTime = 1000;
        /// <summary>
        /// 最大重入数
        /// </summary>
        public const int MaxEnterCount = 10000;
        /// <summary>
        /// 默认超时时间
        /// </summary>
        public const int DefaultTimeout = 3000;
        /// <summary>
        /// 缓存过期时间
        /// </summary>
        public const int CacheExpiredTime = 1000 * 60 * 15;
        /// <summary>
        /// 确认流水线周期时间
        /// </summary>
        public const int AckPipelineInterval = 333;
        /// <summary>
        /// 确认流水线死锁所需次数
        /// </summary>
        public const int AckPipelineDeadlockCount = 3;
        /// <summary>
        /// 用于指定无限长等待时间的常数
        /// </summary>
        public const int Infinite = Timeout.Infinite;

        public static void Ngen(Assembly assembly)
        {
            if (assembly == null)
            {
                throw new ArgumentNullException("assembly");
            }
            foreach (Type type in assembly.GetTypes())
            {
                Ngen(type);
            }
        }

        public static void Ngen<T>()
        {
            Ngen(typeof(T));
        }

        public static void Ngen(Type type)
        {
            if (type == null)
            {
                throw new ArgumentNullException("type");
            }
            foreach (var m in type.GetMethods(BindingFlags.NonPublic | BindingFlags.Public
                 | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            {
                if (m.IsGenericMethod || m.IsAbstract)
                {
                    continue;
                }
                MethodBody body = m.GetMethodBody();
                if (body == null)
                {
                    throw new InvalidProgramException("In a given program there is a case where a method does not have a code-body.");
                }
                byte[] il = body.GetILAsByteArray();
                if (il == null || il.Length <= 0)
                {
                    throw new InvalidProgramException("In the specified program, there is an error function that does not have any il instruction");
                }
                RuntimeMethodHandle handle = m.MethodHandle;
                IntPtr address = handle.GetFunctionPointer();
                if (address == IntPtr.Zero)
                {
                    throw new InvalidProgramException("In the given assembly a method that failed to get the function native-code address was found");
                }
            }
        }

        public static Timer NewTimer()
        {
            return new Timer(scheduler);
        }

        public static MalockClient GetClient(string identity, string mainuseMachine, string standbyMachine)
        {
            return InternalGetObject("Malock->", identity, mainuseMachine, standbyMachine, malocks, () => new MalockClient(identity, mainuseMachine, standbyMachine));
        }

        public static NnsClient GetNns(string identity, string mainuseMachine, string standbyMachine)
        {
            return InternalGetObject("Nns->", identity, mainuseMachine, standbyMachine, nnss, () => new NnsClient(identity, mainuseMachine, standbyMachine));
        }

        private static T InternalGetObject<T>(string category, string identity,
            string standby_machine, string mainuse_machine, Dictionary<string, T> s, Func<T> new_constructor)
        {
            string format = category + "{0}|{1}|{2}";
            string key = string.Format(format, identity, standby_machine, mainuse_machine);
            T item = default(T);
            lock (s)
            {
                if (s.TryGetValue(key, out item))
                {
                    return item;
                }
                key = string.Format(format, identity, mainuse_machine, standby_machine);
                if (s.TryGetValue(key, out item))
                {
                    return item;
                }
                item = new_constructor();
                s.Add(key, item);
            }
            return item;
        }

        public static bool Enter(EventWaitHandle handle)
        {
            return Enter(handle, -1);
        }

        public static bool Enter(EventWaitHandle handle, int millisecondsTimeout)
        {
            if (handle == null)
            {
                throw new ArgumentNullException("handle");
            }
            return handle.TryEnter(millisecondsTimeout);
        }

        public static bool Exit(IEventWaitHandle handle)
        {
            if (handle == null)
            {
                throw new ArgumentNullException("handle");
            }
            return Exit(handle.Handle);
        }

        public static bool Enter(IEventWaitHandle handle, int millisecondsTimeout)
        {
            if (handle == null)
            {
                throw new ArgumentNullException("handle");
            }
            return Enter(handle.Handle, millisecondsTimeout);
        }

        public static bool Enter(IEventWaitHandle handle)
        {
            return Enter(handle, -1);
        }

        public static bool Exit(EventWaitHandle handle)
        {
            if (handle == null)
            {
                throw new ArgumentNullException("handle");
            }
            return handle.Exit();
        }

        public static bool TryGetAllInfo(EventWaitHandle handle, out IEnumerable<HandleInfo> s, ref Exception exception)
        {
            if (handle == null)
            {
                throw new ArgumentNullException("handle");
            }
            return handle.TryGetAllInfo(out s, ref exception);
        }

        public static bool TryGetAllInfo(EventWaitHandle handle, int timeout, out IEnumerable<HandleInfo> s, ref Exception exception)
        {
            if (handle == null)
            {
                throw new ArgumentNullException("handle");
            }
            return handle.TryGetAllInfo(timeout, out s, ref exception);
        }

        public static void GetAllInfoAsync(EventWaitHandle handle, Action<int, IEnumerable<HandleInfo>> callback)
        {
            if (handle == null)
            {
                throw new ArgumentNullException("handle");
            }
            handle.GetAllInfoAsync(callback);
        }

        public static void GetAllInfoAsync(EventWaitHandle handle, int timeout, Action<int, IEnumerable<HandleInfo>> callback)
        {
            if (handle == null)
            {
                throw new ArgumentNullException("handle");
            }
            handle.GetAllInfoAsync(timeout, callback);
        }

        public static int TryGetAllInfo(EventWaitHandle handle, out IEnumerable<HandleInfo> s)
        {
            if (handle == null)
            {
                throw new ArgumentNullException("handle");
            }
            return handle.TryGetAllInfo(out s);
        }

        public static int TryGetAllInfo(EventWaitHandle handle, int timeout, out IEnumerable<HandleInfo> s)
        {
            if (handle == null)
            {
                throw new ArgumentNullException("handle");
            }
            return handle.TryGetAllInfo(timeout, out s);
        }

        public static IEnumerable<HandleInfo> GetAllInfo(IEventWaitHandle handle)
        {
            if (handle == null)
            {
                throw new ArgumentNullException("handle");
            }
            return GetAllInfo(handle.Handle);
        }

        public static IEnumerable<HandleInfo> GetAllInfo(EventWaitHandle handle)
        {
            if (handle == null)
            {
                throw new ArgumentNullException("handle");
            }
            return handle.GetAllInfo();
        }
    }
}
