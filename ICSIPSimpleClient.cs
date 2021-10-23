// ------------------------------------------------------------
// Copyright (c) Ossiaco Inc. All rights reserved.
// ------------------------------------------------------------

namespace Chorus.CSIP
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Chorus.CSIP.Client;
    using Chorus.IEEE2030_5;

    /// <summary>
    /// simple csip client interface.
    /// </summary>
    public interface ICSIPSimpleClient
    {
        /// <summary>
        /// Gets or sets deviceCapability.
        /// </summary>
        DeviceCapability? DeviceCapability { get; set; }

        /// <summary>
        /// Gets or sets endDeviceList.
        /// </summary>
        EndDeviceList? EndDeviceList { get; set; }

        /// <summary>
        /// Gets or sets UsagePointList.
        /// </summary>
        UsagePointList? UsagePointList { get; set; }

        /// <summary>
        /// Gets or sets DERList.
        /// </summary>
        List<DERList?> DERList { get; set; }

        /// <summary>
        /// Gets or sets MirrorUsagePoint.
        /// </summary>
        MirrorUsagePointList? MirrorUsagePointList { get; set; }

        /// <summary>
        /// Gets or sets ResponseSetListLink.
        /// </summary>
        ResponseSetList? ResponseSetList { get; set; }

        /// <summary>
        /// Gets or sets TimeLink.
        /// </summary>
        TimeLink? TimeLink { get; set; }

        /// <summary>
        /// Gets or Sets DERData List.
        /// </summary>
        Dictionary<string, CSIPClientData> DERData { get; set; }

        /// <summary>
        /// Perform HandshakingAsync from server.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        /// <param name="dcapEndpoint">endpoint of device cap.</param>
        Task HandshakingAsync(string dcapEndpoint);
    }
}
