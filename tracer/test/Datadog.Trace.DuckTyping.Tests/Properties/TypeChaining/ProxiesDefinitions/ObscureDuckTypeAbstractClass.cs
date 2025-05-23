// <copyright file="ObscureDuckTypeAbstractClass.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.DuckTyping.Tests.Properties.TypeChaining.ProxiesDefinitions
{
    public abstract class ObscureDuckTypeAbstractClass
    {
        public abstract IDummyFieldObject PublicStaticGetSelfType { get; }

        public abstract IDummyFieldObject InternalStaticGetSelfType { get; }

        public abstract IDummyFieldObject ProtectedStaticGetSelfType { get; }

        public abstract IDummyFieldObject PrivateStaticGetSelfType { get; }

        // *

        public abstract IDummyFieldObject PublicStaticGetSetSelfType { get; set; }

        public abstract IDummyFieldObject InternalStaticGetSetSelfType { get; set; }

        public abstract IDummyFieldObject ProtectedStaticGetSetSelfType { get; set; }

        public abstract IDummyFieldObject PrivateStaticGetSetSelfType { get; set; }

        // *

        public abstract IDummyFieldObject PublicGetSelfType { get; }

        public abstract IDummyFieldObject InternalGetSelfType { get; }

        public abstract IDummyFieldObject ProtectedGetSelfType { get; }

        public abstract IDummyFieldObject PrivateGetSelfType { get; }

        // *

        public abstract IDummyFieldObject PublicGetSetSelfType { get; set; }

        public abstract IDummyFieldObject InternalGetSetSelfType { get; set; }

        public abstract IDummyFieldObject ProtectedGetSetSelfType { get; set; }

        public abstract IDummyFieldObject PrivateGetSetSelfType { get; set; }

        // *
        [Duck(Name = "PublicGetSetSelfType")]
        public abstract ValueWithType<IDummyFieldObject> PublicGetSetSelfTypeWithType { get; set; }
    }
}
