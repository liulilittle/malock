namespace malock.Server
{
    internal enum MalockTaskType : byte
    {
        kEnter,
        kExit,
        kGetAllInfo,
        kAbort,
        kAckPipelineExit,
        kAckPipelineEnter,
    }
}
