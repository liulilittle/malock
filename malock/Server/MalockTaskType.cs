namespace malock.Server
{
    public enum MalockTaskType : byte
    {
        kEnter,
        kExit,
        kGetAllInfo,
        kAbort,
        kAckPipelineExit,
        kAckPipelineEnter,
    }
}
