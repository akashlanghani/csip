// ------------------------------------------------------------
// Copyright (c) Ossiaco Inc. All rights reserved.
// ------------------------------------------------------------

namespace Chorus.CSIP.Client
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Threading.Tasks;
    using System.Xml.Serialization;
    using Chorus.Graph;
    using Chorus.IEEE2030_5;

    /// <summary>
    /// A csip client.
    /// </summary>
    public class TestClient : ICSIPSimpleClient, IDisposable
    {
        private readonly HttpClient client;
        private bool disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="TestClient"/> class.
        /// </summary>
        /// <param name="uri">uri.</param>
        /// <param name="authorizationToken">token.</param>
        public TestClient(string uri, string authorizationToken)
        {
            this.client = new HttpClient();
            this.client.BaseAddress = new Uri(uri);
            this.client.DefaultRequestHeaders.Authorization = AuthenticationHeaderValue.Parse(authorizationToken);
        }

        /// <summary>
        ///  Gets or sets DeviceCapability.
        /// </summary>
        public DeviceCapability? DeviceCapability { get; set; }

        /// <summary>
        ///  Gets or sets EndDeviceList.
        /// </summary>
        public EndDeviceList? EndDeviceList { get; set; }

        /// <summary>
        ///  Gets or sets UsagePointList.
        /// </summary>
        public UsagePointList? UsagePointList { get; set; }

        /// <summary>
        ///  Gets or sets MirrorUsagePointList.
        /// </summary>
        public MirrorUsagePointList? MirrorUsagePointList { get; set; }

        /// <summary>
        ///  Gets or sets ResponseSetList.
        /// </summary>
        public ResponseSetList? ResponseSetList { get; set; }

        /// <summary>
        ///  Gets or sets TimeLink.
        /// </summary>
        public TimeLink? TimeLink { get; set; }

        /// <summary>
        ///  Gets or SetsdERData.
        /// </summary>
        public Dictionary<string, CSIPClientData> DERData { get; set; } = new();

        /// <summary>
        ///  Gets or Sets DERList.
        /// </summary>
        public List<DERList?> DERList { get; set; } = new();

        /// <summary>
        ///  GetDeviceCapabilityAsync.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        /// <param name="endpoint">clientData of device.</param>
        public async Task GetDeviceCapabilityAsync(string endpoint)
        {
            using var response = await this.client.GetAsync(endpoint);
            if (response.Content.Headers.ContentType != null)
            {
                var xml = await response.Content.ToXmlAsync().ConfigureAwait(false);
                this.DeviceCapability = xml.DocumentElement?.GetDeviceCapabilityFromElement();
            }
            else
            {
                throw RequestException.Failed((HttpStatusCode)415);
            }
        }

        /// <summary>
        /// Get end device from server.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public async Task GetEndDeviceAsync()
        {
            if (this.DeviceCapability == null)
            {
                throw new ArgumentNullException(nameof(this.DeviceCapability));
            }

            using var response = await this.client.GetAsync(this.DeviceCapability.EndDeviceListLink?.Href);
            if (response.Content.Headers.ContentType != null)
            {
                var xml = await response.Content.ToXmlAsync().ConfigureAwait(false);
                this.EndDeviceList = xml.DocumentElement?.GetEndDeviceListFromElement();
            }
            else
            {
                throw RequestException.Failed((HttpStatusCode)415);
            }
        }

        /// <summary>
        /// Load all end device data from server.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public async Task LoadEndDeviceDataAsync()
        {
            if (this.EndDeviceList == null)
            {
                throw new ArgumentNullException(nameof(this.EndDeviceList));
            }

            foreach (var item in this.EndDeviceList.EndDevice!)
            {
                var clientData = new CSIPClientData();
                using var response = await this.client.GetAsync(item.FunctionSetAssignmentsListLink?.Href);
                if (response.Content.Headers.ContentType != null)
                {
                    var xml = await response.Content.ToXmlAsync().ConfigureAwait(false);
                    var functionSetAssignmentsListLink = xml.DocumentElement?.GetFunctionSetAssignmentsListLinkFromElement();
                    clientData.FunctionSetAssignmentsList.Add(functionSetAssignmentsListLink!, await GetFunctionSetAssignmentsAsync(functionSetAssignmentsListLink!.Href));
                    await this.LoadDERProgramListAsync(clientData);
                    await this.LoadDerStatusInformationAsync(item);
                    var keyy = item.LFDI;
                    try
                    {
                        this.DERData.Add(keyy!.Value.ToString(), clientData);
                    }
                    catch (Exception ex)
                    {
                        throw new ArgumentNullException(ex.Message);
                    }
                }
                else
                {
                    throw RequestException.Failed((HttpStatusCode)415);
                }
            }

            async Task<FunctionSetAssignmentsList?> GetFunctionSetAssignmentsAsync(Uri uri)
            {
                using var getFunctionSetAssignments = await this.client.GetAsync(uri);
                if (getFunctionSetAssignments.Content.Headers.ContentType != null)
                {
                    var xml = await getFunctionSetAssignments.Content.ToXmlAsync().ConfigureAwait(false);
                    return xml.DocumentElement?.GetFunctionSetAssignmentsListFromElement();
                }
                else
                {
                    throw RequestException.Failed((HttpStatusCode)415);
                }
            }
        }

        /// <summary>
        /// Perform HandshakingAsync from server.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        /// <param name="dcapEndpoint">endpoint of device cap.</param>
        public async Task HandshakingAsync(string dcapEndpoint)
        {
            try
            {
                if (this.client != null)
                {
                    await this.GetDeviceCapabilityAsync(dcapEndpoint);

                    if (this.DeviceCapability != null)
                    {
                        await this.GetEndDeviceAsync();

                        if (this.EndDeviceList != null)
                        {
                            await this.LoadEndDeviceDataAsync();
                        }
                    }
                }
                else
                {
                    throw new ArgumentNullException(nameof(this.client));
                }
            }
            catch (Exception)
            {
                throw new Exception($"Csip Handshaking unsuccessful");
            }
        }

        /// <summary>
        ///  Send Alarm.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        /// <param name="uri"> url to hit.</param>
        /// <param name="logEvent">logEvent to send.</param>
        public async Task SendAlarmAsync(string uri, LogEvent logEvent)
        {
            using var response = await this.client.PostAsync(uri, await logEvent.AsXmlHttpContentAsync());
            string result = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                throw RequestException.Failed((HttpStatusCode)400);
            }
        }

        /// <summary>
        ///  Get Monitor Data.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public async Task GetMonitorDataAsync()
        {
            if (this.DeviceCapability == null)
            {
                throw new ArgumentNullException(nameof(this.DeviceCapability));
            }

            using var response = await this.client.GetAsync(this.DeviceCapability.MirrorUsagePointListLink?.Href);
            if (response.Content.Headers.ContentType != null)
            {
                var xml = await response.Content.ToXmlAsync().ConfigureAwait(false);
                this.MirrorUsagePointList = xml.DocumentElement?.GetMirrorUsagePointListFromElement();
            }
            else
            {
                throw RequestException.Failed((HttpStatusCode)415);
            }
        }

        /// <summary>
        ///  Get usage point Data.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public async Task GetUsagePointAsync()
        {
            if (this.DeviceCapability == null)
            {
                throw new ArgumentNullException(nameof(this.DeviceCapability));
            }

            using var response = await this.client.GetAsync(this.DeviceCapability.UsagePointListLink?.Href);
            if (response.Content.Headers.ContentType != null)
            {
                var xml = await response.Content.ToXmlAsync().ConfigureAwait(false);
                this.UsagePointList = xml.DocumentElement?.GetUsagePointListFromElement();
            }
            else
            {
                throw RequestException.Failed((HttpStatusCode)415);
            }
        }

        /// <summary>
        ///  Get Time Link.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public async Task GetTimeLinkAsync()
        {
            if (this.DeviceCapability == null)
            {
                throw new ArgumentNullException(nameof(this.DeviceCapability));
            }

            using var response = await this.client.GetAsync(this.DeviceCapability.TimeLink?.Href);
            if (response.Content.Headers.ContentType != null)
            {
                var xml = await response.Content.ToXmlAsync().ConfigureAwait(false);
                this.TimeLink = xml.DocumentElement?.GetTimeLinkFromElement();
            }
            else
            {
                throw RequestException.Failed((HttpStatusCode)415);
            }
        }

        /// <summary>
        ///  Send Monitor Data.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        /// <param name="uri"> url to hit.</param>
        /// <param name="mup">logEvent to send.</param>
        public async Task SendMonitorDataAsync(string uri, MirrorUsagePoint mup)
        {
            using var response = await this.client.PostAsync(uri, await mup.AsXmlHttpContentAsync());
            string result = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                throw RequestException.Failed((HttpStatusCode)400);
            }
        }

        /// <summary>
        ///  Send DER Status.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        /// <param name="uri"> url to hit.</param>
        /// <param name="status">status to send.</param>
        public async Task SendDERStatusAsync(string uri, DERStatus status)
        {
            using var response = await this.client.PutAsync(uri, await status.AsXmlHttpContentAsync());
            string result = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                throw RequestException.Failed((HttpStatusCode)400);
            }
        }

        /// <summary>
        ///  Send DER Cap .
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        /// <param name="uri"> url to hit.</param>
        /// <param name="cap">cap to send.</param>
        public async Task SendDERCapAsync(string uri, DERCapability cap)
        {
            using var response = await this.client.PutAsync(uri, await cap.AsXmlHttpContentAsync());
            string result = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                throw RequestException.Failed((HttpStatusCode)400);
            }
        }

        /// <summary>
        ///  Send DER Setting .
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        /// <param name="uri"> url to hit.</param>
        /// <param name="derg">derg to send.</param>
        public async Task SendDERCapAsync(string uri, DERSettings derg)
        {
            using var response = await this.client.PutAsync(uri, await derg.AsXmlHttpContentAsync());
            string result = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                throw RequestException.Failed((HttpStatusCode)400);
            }
        }

        /// <summary>
        ///  Send DER Availability .
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        /// <param name="uri"> url to hit.</param>
        /// <param name="dera">dera to send.</param>
        public async Task SendDERCapAsync(string uri, DERAvailability dera)
        {
            using var response = await this.client.PutAsync(uri, await dera.AsXmlHttpContentAsync());
            string result = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                throw RequestException.Failed((HttpStatusCode)400);
            }
        }

        /// <summary>
        /// Dispose http client.
        /// </summary>
        public virtual void Dispose()
        {
            if (this.disposed)
            {
                return;
            }

            this.disposed = true;
            this.client.Dispose();
        }

        /// <summary>
        /// Get Der Status informtion from server.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        /// <param name="endDevice">endDevice.</param>
        private async Task LoadDerStatusInformationAsync(EndDevice endDevice)
        {
            if (endDevice == null)
            {
                throw new ArgumentNullException(nameof(this.DeviceCapability));
            }

            using var response = await this.client.GetAsync(endDevice.DERListLink?.Href);
            if (response.Content.Headers.ContentType != null)
            {
                var xml = await response.Content.ToXmlAsync().ConfigureAwait(false);
                this.DERList.Add(xml.DocumentElement?.GetDERListFromElement());
            }
            else
            {
                throw RequestException.Failed((HttpStatusCode)415);
            }
        }

        /// <summary>
        /// load LoadDERProgramListAsync of FSA from server.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        /// <param name="clientData">clientData of device.</param>
        private async Task LoadDERProgramListAsync(CSIPClientData clientData)
        {
            if (clientData.FunctionSetAssignmentsList == null)
            {
                throw new ArgumentNullException(nameof(clientData.FunctionSetAssignmentsList));
            }

            foreach (var item in clientData.FunctionSetAssignmentsList.Values)
            {
                foreach (var fsa in item?.FunctionSetAssignments!)
                {
                    using var response = await this.client.GetAsync(fsa.DERProgramListLink?.Href);
                    if (response.Content.Headers.ContentType != null)
                    {
                        var xml = await response.Content.ToXmlAsync().ConfigureAwait(false);
                        clientData.DERProgramList.Add(xml.DocumentElement!.GetDERProgramListFromElement());
                        await this.LoadDERProgramAsync(clientData);
                    }
                    else
                    {
                        throw RequestException.Failed((HttpStatusCode)415);
                    }
                }
            }
        }

        /// <summary>
        /// load DERProgram of FSA from server.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        /// <param name="clientData">clientData of device.</param>
        private async Task LoadDERProgramAsync(CSIPClientData clientData)
        {
            if (clientData.DERProgramList == null)
            {
                throw new ArgumentNullException(nameof(clientData.DERProgramList));
            }

            // Load ActiveDERControlListLink
            foreach (var item in clientData.DERProgramList!)
            {
                foreach (var derProgram in item.DERProgram!)
                {
                    using var activeDERControlListLink = await this.client.GetAsync(derProgram.ActiveDERControlListLink?.Href);
                    if (activeDERControlListLink.Content.Headers.ContentType != null)
                    {
                        var xml = await activeDERControlListLink.Content.ToXmlAsync().ConfigureAwait(false);
                        clientData.ActiveDERControlsList = xml.DocumentElement!.GetActiveDERControlListLinkFromElement();
                    }

                    // Load DefaultDERControlLink
                    using var defaultDERControlLink = await this.client.GetAsync(derProgram.DefaultDERControlLink?.Href);
                    if (defaultDERControlLink.Content.Headers.ContentType != null)
                    {
                        var xml = await defaultDERControlLink.Content.ToXmlAsync().ConfigureAwait(false);
                        var defualtDerControlList = xml.DocumentElement!.GetDefaultDERControlFromElement();
                        clientData.DefaultDERControlList.Add(derProgram.DefaultDERControlLink!, xml.DocumentElement!.GetDefaultDERControlFromElement());
                    }

                    // Load DERControlListLink
                    using var responseDERControlListLink = await this.client.GetAsync(derProgram.DERControlListLink?.Href);
                    if (responseDERControlListLink.Content.Headers.ContentType != null)
                    {
                        var xml = await responseDERControlListLink.Content.ToXmlAsync().ConfigureAwait(false);
                        clientData.DERControlList.Add(derProgram.DERControlListLink!, xml.DocumentElement!.GetDERControlListFromElement());
                    }

                    // Load DERCurveList
                    using var responseDERCurveListLink = await this.client.GetAsync(derProgram.DERCurveListLink?.Href);
                    if (responseDERCurveListLink.Content.Headers.ContentType != null)
                    {
                        var xml = await responseDERCurveListLink.Content.ToXmlAsync().ConfigureAwait(false);
                        clientData.DERCurveList.Add(derProgram.DERCurveListLink!, xml.DocumentElement!.GetDERCurveListFromElement());
                    }
                }
            }
        }
    }
}
