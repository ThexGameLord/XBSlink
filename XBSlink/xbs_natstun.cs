﻿/**
 * Project: XBSlink: A XBox360 & PS3/2 System Link Proxy
 * File name: xbs_natstun.cs
 *   
 * @author Oliver Seuffert, Copyright (C) 2011.
 */
/* 
 * XBSlink is free software; you can redistribute it and/or modify 
 * it under the terms of the GNU General Public License as published by 
 * the Free Software Foundation; either version 2 of the License, or 
 * (at your option) any later version.
 * 
 * This program is distributed in the hope that it will be useful, but 
 * WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY 
 * or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License 
 * for more details.
 * 
 * You should have received a copy of the GNU General Public License along 
 * with this program; If not, see <http://www.gnu.org/licenses/>
 */

using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Mono.Nat;
using Mono.Nat.Pmp;
using Mono.Nat.Upnp;

namespace XBSlink
{
    class xbs_natstun
    {
        // NAT variables
        private INatDevice device = null;
        private IPAddress public_ip = null;
        private List<Mapping> my_PortMappings = new List<Mapping>();
        private bool upnp_discovery_started = false;

        public static bool isPortReachable = false;

        // External IP Service
        private const String EXTERNAL_IP_WEB_SERVICE = "http://www.whatismyip.com/automation/n09230945.asp";

#if DEBUG
        class UPnPlogger : StringWriter
        {
            public override void WriteLine(string format, params object[] arg)
            {
                base.WriteLine(format, arg);
                TextReader stringReader = new StringReader(this.ToString());
                String[] s = stringReader.ReadToEnd().Split(Environment.NewLine.ToCharArray());
                //for (int i = 0; i < s.Length; i++) if (s[i].Trim().Length > 0) xbs_messages.addDebugMessage(" @ UPnP log: " + s[i].Trim());
            }
        }
#endif
        public xbs_natstun()
        {
#if DEBUG
            NatUtility.Logger = new UPnPlogger();
#endif
            NatUtility.DeviceFound += upnp_device_found;
            NatUtility.UnhandledException += upnp_unhandled_exception;
        }

        #region --------- NAT stuff ---------

        public void upnp_startDiscovery()
        {
            if (!isUPnPavailable())
            {
                xbs_messages.addInfoMessage(" @ UPnP device discovery started");
                NatUtility.StartDiscovery();
                upnp_discovery_started = true;
            }
        }

        public void upnp_stopDiscovery()
        {
            if (upnp_discovery_started)
            {
                xbs_messages.addDebugMessage(" @ UPnP device discovery stopped");
                NatUtility.StopDiscovery();
            }
        }

        private void upnp_device_found(object sender, DeviceEventArgs args)
        {
            INatDevice dev = args.Device;
            IPAddress pub_ip = null;
            try
            {
                pub_ip = dev.GetExternalIP();
            }
            catch (Exception)
            {
                device = null;
                public_ip = null;
            }
            if (dev!=null && pub_ip!=null)
            {
                lock (this)
                {
                    this.device = dev;
                    this.public_ip = pub_ip;
                }
                xbs_messages.addInfoMessage(" @ UPnP device found. external IP: " + pub_ip);
            }
            else
                xbs_messages.addInfoMessage(" @ UPnP discovery failed. Could not get public IP");
        }

        public bool upnp_create_mapping(Protocol prot, int internalPort, int externalPort)
        {
            if (isUPnPavailable())
            {
                Mapping port_mapping = new Mapping(prot, internalPort, externalPort);
                try
                {
                    device.CreatePortMap(port_mapping);
                }
                catch (MappingException)
                {
                    xbs_messages.addInfoMessage(" @ UPnP error: could not forward port");
                    return false;
                }
                lock (this)
                    my_PortMappings.Add(port_mapping);
                xbs_messages.addInfoMessage(" @ UPnP port mapped from " + public_ip + ":" + port_mapping.PublicPort);
                return true;
            }
            return false;
        }

        public void upnp_deleteAllPortMappings()
        {
            if (isUPnPavailable())
            {
                List<Mapping> mappings;
                lock (this)
                {
                    mappings = this.my_PortMappings;
                    this.my_PortMappings = new List<Mapping>();
                }
                try
                {
                    foreach (Mapping pm in mappings)
                    {
                        device.DeletePortMap(pm);
                        xbs_messages.addInfoMessage(" @ UPnP port mapping removed " + public_ip + ":" + pm.PublicPort);
                    }
                }
                catch (Exception)
                {
                }
            }
        }

        private void upnp_unhandled_exception(object sender, UnhandledExceptionEventArgs args)
        {
            xbs_messages.addInfoMessage(" @ UPnP error: " + args.ExceptionObject.ToString());
        }

        public IPAddress upnp_getPublicIP()
        {
            return public_ip;
        }

        public bool isUPnPavailable()
        {
            lock (this)
                return (this.device != null);
        }
        #endregion

        public static IPAddress getExternalIPAddressFromWebsite()
        {
            String external_ip_str;
            IPAddress external_ip;
            try
            {
                external_ip_str = new System.Net.WebClient().DownloadString(xbs_natstun.EXTERNAL_IP_WEB_SERVICE);
            }
            catch (WebException)
            {
                xbs_messages.addInfoMessage("!! Could not resolve external IP Address.");
                return null;
            }
            bool ret = IPAddress.TryParse(external_ip_str.Trim(), out external_ip);
            if ( ret )
                xbs_messages.addInfoMessage(" @ discovered external public IP " + external_ip);
            return ( ret ? external_ip : null);
        }

    }
}
