/*
* sones GraphDB - Community Edition - http://www.sones.com
* Copyright (C) 2007-2011 sones GmbH
*
* This file is part of sones GraphDB Community Edition.
*
* sones GraphDB is free software: you can redistribute it and/or modify
* it under the terms of the GNU Affero General Public License as published by
* the Free Software Foundation, version 3 of the License.
* 
* sones GraphDB is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
* GNU Affero General Public License for more details.
*
* You should have received a copy of the GNU Affero General Public License
* along with sones GraphDB. If not, see <http://www.gnu.org/licenses/>.
* 
*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using sones.Library.UserdefinedDataType;
using sones.Library.NewFastSerializer;
using sones.Library.VersionedPluginManager;
using sones.Library.LanguageExtensions;
using System.Net;

namespace IPAddress
{
    /// <summary>
    /// This is a class that implement the user defined data type for ip addresses
    /// </summary>
    public class IPAddress : AUserdefinedDataType
    {
        #region Data

        private System.Net.IPAddress _IPAddress;

        #endregion

        #region Constructors

        public IPAddress()
        {   
        }

        public IPAddress(IComparable value) : base(value)
        { }

        #endregion

        public override string TypeName
        {
            get { return "IPAddress"; }
        }

        public override void Serialize(ref SerializationWriter mySerializationWriter)
        {
            mySerializationWriter.CheckNull("mySerializationWriter");
            
            if (_IPAddress != null)
            {
                mySerializationWriter.WriteBytesDirect(_IPAddress.GetAddressBytes());
            }
        }

        public override void Deserialize(ref SerializationReader mySerializationReader)
        {
            mySerializationReader.CheckNull("mySerializationReader");

            _IPAddress = new System.Net.IPAddress(mySerializationReader.ReadByteArray());
        }

        public override int CompareTo(object obj)
        {
            this._IPAddress.CheckNull("_IPAddress");
            
            obj.CheckNull("obj");
            
            IPAddress address = obj as IPAddress;

            if (address == null)
            {
                throw new ArgumentException("The parameter ob is not of type ipaddress.");
            }

            return this._IPAddress.ToString().CompareTo(address._IPAddress.ToString());
        }

        public override string PluginName
        {
            get { return "sones.ipaddress"; }
        }

        public override string PluginShortName
        {
            get { return "ipaddress"; }
        }

        public override string PluginDescription
        {
            get { return "This class realize an user defined data type ip address."; }
        }

        public override PluginParameters<Type> SetableParameters
        {
            get { return new PluginParameters<Type>(); }
        }

        public override IPluginable InitializePlugin(string UniqueString, Dictionary<string, object> myParameters = null)
        {
            return (IPluginable)(new IPAddress());
        }

        public override void Dispose()
        {            
        }

        public override string Value
        {
            get
            {
                return _IPAddress == null ? String.Empty : _IPAddress.ToString();
            }
            set
            {
                _IPAddress = Dns.GetHostAddresses(value).First();
            }
        }
    }
}
