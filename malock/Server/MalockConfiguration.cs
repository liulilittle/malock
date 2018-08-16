namespace malock.Server
{
    using System;

    public class MalockConfiguration
    {
        public string Identity
        {
            get;
            private set;
        }

        public string NnsId
        {
            get;
            private set;
        }

        public int Port
        {
            get;
            private set;
        }

        public string StandbyNode
        {
            get;
            private set;
        }

        public string NnsNode
        {
            get;
            private set;
        }

        public string NnsStandbyNode
        {
            get;
            private set;
        }

        public object Tag
        {
            get;
            set;
        }

        public MalockConfiguration(string identity, int port, string nns, string standbyNode, string nnsNode, string nnsStandbyNode)
        {
            if (port <= 0 || port > short.MaxValue)
            {
                throw new ArgumentOutOfRangeException("The specified server listening port is outside the 0~65535 range");
            }
            if (string.IsNullOrEmpty(standbyNode))
            {
                throw new ArgumentOutOfRangeException("You have specified an invalid standby server host address that is not allowed to be null or empty");
            }
            if (string.IsNullOrEmpty(nnsNode))
            {
                throw new ArgumentOutOfRangeException("You have specified an invalid NNS host");
            }
            if (string.IsNullOrEmpty(identity))
            {
                throw new ArgumentOutOfRangeException("You have specified an invalid node Identity");
            }
            if (string.IsNullOrEmpty(nns))
            {
                throw new ArgumentOutOfRangeException("Must provide a valid NNS-Id category domain name");
            }
            if (string.IsNullOrEmpty(nnsStandbyNode))
            {
                throw new ArgumentOutOfRangeException("You have specified an invalid NNS-standby host");
            }
            this.Identity = identity;
            this.NnsNode = nnsNode;
            this.NnsId = nns;
            this.NnsStandbyNode = nnsStandbyNode;
            this.Port = port;
            this.StandbyNode = standbyNode;
        }
    }
}
