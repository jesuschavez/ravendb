﻿using System.Collections.Generic;

namespace Raven.Client.Server.ETL
{
    public abstract class EtlDestination
    {
        public abstract bool Validate(ref List<string> errors);
    }
}