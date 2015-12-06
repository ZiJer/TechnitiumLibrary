﻿/*
Technitium Library
Copyright (C) 2015  Shreyas Zare (shreyas@technitium.com)

This program is free software: you can redistribute it and/or modify
it under the terms of the GNU General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with this program.  If not, see <http://www.gnu.org/licenses/>.

*/

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace TechnitiumLibrary.Net
{
    public class NetUtilities
    {
        #region static

        public static bool IsPrivateIP(IPAddress address)
        {
            switch (address.AddressFamily)
            {
                case AddressFamily.InterNetwork:
                    return IsPrivateIPv4(address);

                case AddressFamily.InterNetworkV6:
                    return !IsPublicIPv6(address);

                default:
                    throw new NotSupportedException("Address family not supported.");
            }
        }

        public static bool IsPrivateIPv4(IPAddress address)
        {
            //127.0.0.0 - 127.255.255.255
            //10.0.0.0 - 10.255.255.255
            //169.254.0.0 - 169.254.255.255
            //172.16.0.0 - 172.31.255.255
            //192.168.0.0 - 192.168.255.255

            if (address.AddressFamily != AddressFamily.InterNetwork)
                throw new ArgumentException("IPv4 address expected.");

            byte[] ip = address.GetAddressBytes();

            switch (ip[0])
            {
                case 127:
                case 10:
                    return true;

                case 169:
                    return (ip[1] == 254);

                case 172:
                    return ((ip[1] >= 16) && (ip[1] <= 31));

                case 192:
                    return (ip[1] == 168);

                default:
                    return false;
            }
        }

        public static bool IsPublicIPv6(IPAddress address)
        {
            //2000::/3 --> Global Unicast

            if (address.AddressFamily != AddressFamily.InterNetworkV6)
                throw new ArgumentException("IPv6 address expected.");

            byte[] ip = address.GetAddressBytes();

            return ((ip[0] & 0x20) == 0x20);
        }

        public static NetworkInfo GetDefaultNetworkInfo()
        {
            foreach (NetworkInterface nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (nic.OperationalStatus != OperationalStatus.Up)
                    continue;

                IPInterfaceProperties ipInterface = nic.GetIPProperties();

                foreach (UnicastIPAddressInformation ip in ipInterface.UnicastAddresses)
                {
                    switch (ip.Address.AddressFamily)
                    {
                        case AddressFamily.InterNetwork:
                            #region ipv4
                            {
                                byte[] addr = ip.Address.GetAddressBytes();
                                byte[] mask;

                                try
                                {
                                    mask = ip.IPv4Mask.GetAddressBytes();
                                }
                                catch (NotImplementedException)
                                {
                                    //method not implemented in mono framework for Linux
                                    if (addr[0] == 10)
                                    {
                                        mask = new byte[] { 255, 0, 0, 0 };
                                    }
                                    else if ((addr[0] == 192) && (addr[1] == 168))
                                    {
                                        mask = new byte[] { 255, 255, 255, 0 };
                                    }
                                    else if ((addr[0] == 169) && (addr[1] == 254))
                                    {
                                        mask = new byte[] { 255, 255, 0, 0 };
                                    }
                                    else if ((addr[0] == 172) && (addr[1] > 15) && (addr[1] < 32))
                                    {
                                        mask = new byte[] { 255, 240, 0, 0 };
                                    }
                                    else
                                    {
                                        mask = new byte[] { 255, 255, 255, 0 };
                                    }
                                }
                                catch
                                {
                                    continue;
                                }

                                foreach (GatewayIPAddressInformation gateway in ipInterface.GatewayAddresses)
                                {
                                    if (gateway.Address.AddressFamily == AddressFamily.InterNetwork)
                                    {
                                        byte[] gatewayAddr = gateway.Address.GetAddressBytes();
                                        bool isDefaultRoute = true;
                                        bool isInSameNetwork = true;

                                        for (int i = 0; i < 4; i++)
                                        {
                                            if (gatewayAddr[i] != 0)
                                            {
                                                isDefaultRoute = false;
                                                break;
                                            }
                                        }

                                        if (isDefaultRoute)
                                            return new NetworkInfo(nic.NetworkInterfaceType, ip.Address, new IPAddress(mask));

                                        for (int i = 0; i < 4; i++)
                                        {
                                            if ((addr[i] & mask[i]) != (gatewayAddr[i] & mask[i]))
                                            {
                                                isInSameNetwork = false;
                                                break;
                                            }
                                        }

                                        if (isInSameNetwork)
                                            return new NetworkInfo(nic.NetworkInterfaceType, ip.Address, new IPAddress(mask));
                                    }
                                }
                            }
                            #endregion
                            break;

                        case AddressFamily.InterNetworkV6:
                            #region ipv6
                            {
                                if (IsPublicIPv6(ip.Address))
                                {
                                    if (ipInterface.GatewayAddresses.Count > 0)
                                    {
                                        byte[] addr = ip.Address.GetAddressBytes();
                                        bool isValidRoute = true;

                                        foreach (GatewayIPAddressInformation gateway in ipInterface.GatewayAddresses)
                                        {
                                            if (gateway.Address.AddressFamily == AddressFamily.InterNetworkV6)
                                            {
                                                byte[] gatewayAddr = gateway.Address.GetAddressBytes();

                                                for (int i = 0; i < 8; i++)
                                                {
                                                    if (addr[i] != gatewayAddr[i])
                                                    {
                                                        isValidRoute = false;
                                                        break;
                                                    }
                                                }

                                                if (isValidRoute)
                                                    return new NetworkInfo(nic.NetworkInterfaceType, ip.Address);
                                            }
                                        }
                                    }
                                }
                            }
                            #endregion
                            break;
                    }
                }
            }

            return null;
        }

        public static List<NetworkInfo> GetNetworkInfo(AddressFamily addressFamily)
        {
            List<NetworkInfo> networkInfoList = new List<NetworkInfo>(3);

            foreach (NetworkInterface nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (nic.OperationalStatus != OperationalStatus.Up)
                    continue;

                foreach (UnicastIPAddressInformation ip in nic.GetIPProperties().UnicastAddresses)
                {
                    if (ip.Address.AddressFamily == addressFamily)
                    {
                        switch (ip.Address.AddressFamily)
                        {
                            case AddressFamily.InterNetwork:
                                #region ipv4

                                byte[] addr = ip.Address.GetAddressBytes();
                                byte[] mask;

                                try
                                {
                                    mask = ip.IPv4Mask.GetAddressBytes();
                                }
                                catch (NotImplementedException)
                                {
                                    //method not implemented in mono framework for Linux
                                    if (addr[0] == 10)
                                    {
                                        mask = new byte[] { 255, 0, 0, 0 };
                                    }
                                    else if ((addr[0] == 192) && (addr[1] == 168))
                                    {
                                        mask = new byte[] { 255, 255, 255, 0 };
                                    }
                                    else if ((addr[0] == 169) && (addr[1] == 254))
                                    {
                                        mask = new byte[] { 255, 255, 0, 0 };
                                    }
                                    else if ((addr[0] == 172) && (addr[1] > 15) && (addr[1] < 32))
                                    {
                                        mask = new byte[] { 255, 240, 0, 0 };
                                    }
                                    else
                                    {
                                        mask = new byte[] { 255, 255, 255, 0 };
                                    }
                                }
                                catch
                                {
                                    continue;
                                }

                                networkInfoList.Add(new NetworkInfo(nic.NetworkInterfaceType, ip.Address, new IPAddress(mask)));

                                #endregion
                                break;

                            case AddressFamily.InterNetworkV6:
                                #region ipv6

                                networkInfoList.Add(new NetworkInfo(nic.NetworkInterfaceType, ip.Address));
                                
                                #endregion
                                break;
                        }
                    }
                }
            }

            return networkInfoList;
        }

        public static NetworkInfo GetNetworkInfo(IPAddress destinationIP)
        {
            byte[] destination = destinationIP.GetAddressBytes();

            foreach (NetworkInterface nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (nic.OperationalStatus != OperationalStatus.Up)
                    continue;

                IPInterfaceProperties ipInterface = nic.GetIPProperties();

                foreach (UnicastIPAddressInformation ip in ipInterface.UnicastAddresses)
                {
                    if (ip.Address.AddressFamily == destinationIP.AddressFamily)
                    {
                        switch (destinationIP.AddressFamily)
                        {
                            case AddressFamily.InterNetwork:
                                #region ipv4
                                {
                                    byte[] addr = ip.Address.GetAddressBytes();
                                    byte[] mask;

                                    try
                                    {
                                        mask = ip.IPv4Mask.GetAddressBytes();
                                    }
                                    catch (NotImplementedException)
                                    {
                                        //method not implemented in mono framework for Linux
                                        if (addr[0] == 10)
                                        {
                                            mask = new byte[] { 255, 0, 0, 0 };
                                        }
                                        else if ((addr[0] == 192) && (addr[1] == 168))
                                        {
                                            mask = new byte[] { 255, 255, 255, 0 };
                                        }
                                        else if ((addr[0] == 169) && (addr[1] == 254))
                                        {
                                            mask = new byte[] { 255, 255, 0, 0 };
                                        }
                                        else if ((addr[0] == 172) && (addr[1] > 15) && (addr[1] < 32))
                                        {
                                            mask = new byte[] { 255, 240, 0, 0 };
                                        }
                                        else
                                        {
                                            mask = new byte[] { 255, 255, 255, 0 };
                                        }
                                    }
                                    catch
                                    {
                                        continue;
                                    }

                                    bool isInSameNetwork = true;

                                    for (int i = 0; i < 4; i++)
                                    {
                                        if ((addr[i] & mask[i]) != (destination[i] & mask[i]))
                                        {
                                            isInSameNetwork = false;
                                            break;
                                        }
                                    }

                                    if (isInSameNetwork)
                                        return new NetworkInfo(nic.NetworkInterfaceType, ip.Address, new IPAddress(mask));
                                }
                                #endregion
                                break;

                            case AddressFamily.InterNetworkV6:
                                #region ipv6
                                {
                                    byte[] addr = ip.Address.GetAddressBytes();
                                    bool isInSameNetwork = true;

                                    for (int i = 0; i < 8; i++)
                                    {
                                        if (addr[i] != destination[i])
                                        {
                                            isInSameNetwork = false;
                                            break;
                                        }
                                    }

                                    if (isInSameNetwork)
                                        return new NetworkInfo(nic.NetworkInterfaceType, ip.Address);
                                }
                                #endregion
                                break;
                        }
                    }
                }
            }

            return GetDefaultNetworkInfo();
        }

        #endregion
    }

    public class NetworkInfo
    {
        #region variables

        NetworkInterfaceType _type;
        IPAddress _localIP;
        IPAddress _subnetMask;
        IPAddress _broadcastIP;

        #endregion

        #region constructor

        public NetworkInfo(NetworkInterfaceType type, IPAddress localIP)
        {
            if (localIP.AddressFamily != AddressFamily.InterNetworkV6)
                throw new NotSupportedException("Address family not supported.");

            _type = type;
            _localIP = localIP;
        }

        public NetworkInfo(NetworkInterfaceType type, IPAddress localIP, IPAddress subnetMask)
        {
            if (localIP.AddressFamily != AddressFamily.InterNetwork)
                throw new NotSupportedException("Address family not supported.");

            _type = type;
            _localIP = localIP;
            _subnetMask = subnetMask;

            byte[] broadcast = new byte[4];
            byte[] addr = localIP.GetAddressBytes();
            byte[] mask = subnetMask.GetAddressBytes();

            for (int i = 0; i < 4; i++)
                broadcast[i] = (byte)(addr[i] | ~mask[i]);

            _broadcastIP = new IPAddress(broadcast);
        }

        #endregion

        #region public

        public override bool Equals(object obj)
        {
            return Equals(obj as NetworkInfo);
        }

        public bool Equals(NetworkInfo obj)
        {
            if (ReferenceEquals(null, obj))
                return false;

            if (ReferenceEquals(this, obj))
                return true;

            if (_type != obj._type)
                return false;

            if (!_localIP.Equals(obj._localIP))
                return false;

            return true;
        }

        public override int GetHashCode()
        {
            return BitConverter.ToInt32(_localIP.GetAddressBytes(), 0);
        }

        #endregion

        #region properties

        public NetworkInterfaceType InterfaceType
        { get { return _type; } }

        public IPAddress LocalIP
        { get { return _localIP; } }

        public IPAddress SubnetMask
        { get { return _subnetMask; } }

        public IPAddress BroadcastIP
        { get { return _broadcastIP; } }

        #endregion
    }
}