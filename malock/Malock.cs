namespace malock
{
    using global::malock.Client;
    using global::malock.Core;
    using System;
    using System.Collections.Generic;
    using System.Reflection;

    public static class Malock
    {
        private static readonly TimerScheduler scheduler = new TimerScheduler();
        /// <summary>
        /// 连接中断后重连间隔时间
        /// </summary>
        public const int ReconnectionTime = 500;
        /// <summary>
        /// 连接中断后平滑到另一个连接的平滑时间
        /// </summary>
        public const int SmoothingTime = 500;
        /// <summary>
        /// 最大重入数
        /// </summary>
        public const int MaxEnterCount = 10000;

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

        public static MalockClient NewClient(string identity, string mainuseMachine, string standbyMachine)
        {
            return new MalockClient(identity, mainuseMachine, standbyMachine);
        }

        public static bool Enter(EventWaitHandle handle)
        {
            return Enter(handle, -1);
        }

        public static bool Enter(EventWaitHandle handle, int millisecondsTimeout)
        {
            return handle.TryEnter(millisecondsTimeout);
        }

        public static bool Exit(EventWaitHandle handle)
        {
            return handle.Exit();
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
