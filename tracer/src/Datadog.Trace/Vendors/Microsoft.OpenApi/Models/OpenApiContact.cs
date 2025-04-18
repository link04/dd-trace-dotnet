//------------------------------------------------------------------------------
// <auto-generated />
// This file was automatically generated by the UpdateVendoredCode tool.
//------------------------------------------------------------------------------
#pragma warning disable CS0618, CS0649, CS1574, CS1580, CS1581, CS1584, CS1591, CS1573, CS8018, SYSLIB0011, SYSLIB0023, SYSLIB0032
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.

using System;
using System.Collections.Generic;
using Datadog.Trace.Vendors.Microsoft.OpenApi.Interfaces;
using Datadog.Trace.Vendors.Microsoft.OpenApi.Writers;

namespace Datadog.Trace.Vendors.Microsoft.OpenApi.Models
{
    /// <summary>
    /// Contact Object.
    /// </summary>
    internal class OpenApiContact : IOpenApiSerializable, IOpenApiExtensible
    {
        /// <summary>
        /// The identifying name of the contact person/organization.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// The URL pointing to the contact information. MUST be in the format of a URL.
        /// </summary>
        public Uri Url { get; set; }

        /// <summary>
        /// The email address of the contact person/organization.
        /// MUST be in the format of an email address.
        /// </summary>
        public string Email { get; set; }

        /// <summary>
        /// This object MAY be extended with Specification Extensions.
        /// </summary>
        public IDictionary<string, IOpenApiExtension> Extensions { get; set; } = new Dictionary<string, IOpenApiExtension>();

        /// <summary>
        /// Parameter-less constructor
        /// </summary>
        public OpenApiContact() { }

        /// <summary>
        /// Initializes a copy of an <see cref="OpenApiContact"/> instance
        /// </summary>
        public OpenApiContact(OpenApiContact contact)
        {
            Name = contact?.Name ?? Name;
            Url = contact?.Url != null ? new Uri(contact.Url.OriginalString, UriKind.RelativeOrAbsolute) : null;
            Email = contact?.Email ?? Email;
            Extensions = contact?.Extensions != null ? new Dictionary<string, IOpenApiExtension>(contact.Extensions) : null;
        }

        /// <summary>
        /// Serialize <see cref="OpenApiContact"/> to Open Api v3.0
        /// </summary>
        public void SerializeAsV3(IOpenApiWriter writer)
        {
            WriteInternal(writer, OpenApiSpecVersion.OpenApi3_0);
        }

        /// <summary>
        /// Serialize <see cref="OpenApiContact"/> to Open Api v2.0
        /// </summary>
        public void SerializeAsV2(IOpenApiWriter writer)
        {
            WriteInternal(writer, OpenApiSpecVersion.OpenApi2_0);
        }

        private void WriteInternal(IOpenApiWriter writer, OpenApiSpecVersion specVersion)
        {
            writer.WriteStartObject();

            // name
            writer.WriteProperty(OpenApiConstants.Name, Name);

            // url
            writer.WriteProperty(OpenApiConstants.Url, Url?.OriginalString);

            // email
            writer.WriteProperty(OpenApiConstants.Email, Email);

            // extensions
            writer.WriteExtensions(Extensions, specVersion);

            writer.WriteEndObject();
        }
    }
}
