﻿// <copyright file="CIVisibilityEventMessagePackFormatter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using Datadog.Trace.Ci.EventModel;
using Datadog.Trace.Vendors.MessagePack;

namespace Datadog.Trace.Ci.Agent.MessagePack;

internal class CIVisibilityEventMessagePackFormatter<T> : EventMessagePackFormatter<CIVisibilityEvent<T>>
{
    public override int Serialize(ref byte[] bytes, int offset, CIVisibilityEvent<T> value, IFormatterResolver formatterResolver)
    {
        if (value is null)
        {
            Log.Warning("CIVisibilityEventMessagePackFormatter<{Type}>: event is null", typeof(T).Name);
            return 0;
        }

        var originalOffset = offset;

        offset += MessagePackBinary.WriteMapHeader(ref bytes, offset, 3);

        offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, TypeBytes);
        offset += MessagePackBinary.WriteString(ref bytes, offset, value.Type);

        offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, VersionBytes);
        offset += MessagePackBinary.WriteInt32(ref bytes, offset, value.Version);

        offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, ContentBytes);
        offset += formatterResolver.GetFormatter<T>().Serialize(ref bytes, offset, value.Content, formatterResolver);

        return offset - originalOffset;
    }
}
