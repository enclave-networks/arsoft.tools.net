﻿#region Copyright and License
// Copyright 2010..2017 Alexander Reinert
// 
// This file is part of the ARSoft.Tools.Net - C# DNS client/server and SPF Library (https://github.com/alexreinert/ARSoft.Tools.Net)
// 
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
//   http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
#endregion

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace ARSoft.Tools.Net.Dns
{
	/// <summary>
	///   Base class representing a dns record
	/// </summary>
	public abstract class DnsRecordBase : DnsMessageEntryBase, IComparable<DnsRecordBase>, IEquatable<DnsRecordBase>
	{
		internal int StartPosition { get; set; }
		internal ushort RecordDataLength { get; set; }

		/// <summary>
		///   Seconds which a record should be cached at most
		/// </summary>
		public int TimeToLive { get; internal set; }

		protected DnsRecordBase() {}

		protected DnsRecordBase(DomainName name, RecordType recordType, RecordClass recordClass, int timeToLive)
		{
			if (name == null)
				throw new ArgumentNullException(nameof(name));

			Name = name;
			RecordType = recordType;
			RecordClass = recordClass;
			TimeToLive = timeToLive;
		}

		internal static DnsRecordBase Create(RecordType type, byte[] resultData, int recordDataPosition)
		{
		    return Create(type);
        }

		internal static DnsRecordBase Create(RecordType type)
		{
			switch (type)
			{
				case RecordType.A:
					return new ARecord();
				case RecordType.Ns:
					return new NsRecord();
				case RecordType.CName:
					return new CNameRecord();
				case RecordType.Soa:
					return new SoaRecord();
				case RecordType.Ptr:
					return new PtrRecord();
				case RecordType.Mx:
					return new MxRecord();
				case RecordType.Txt:
					return new TxtRecord();
				case RecordType.Rp:
					return new RpRecord();
				case RecordType.Aaaa:
					return new AaaaRecord();
				case RecordType.Loc:
					return new LocRecord();
				case RecordType.Srv:
					return new SrvRecord();
				case RecordType.Naptr:
					return new NaptrRecord();
				case RecordType.Eui48:
					return new Eui48Record();
				case RecordType.Eui64:
					return new Eui64Record();

				default:
					return new UnknownRecord();
			}
		}

		#region ToString
		internal abstract string RecordDataToString();

		/// <summary>
		///   Returns the textual representation of a record
		/// </summary>
		/// <returns> Textual representation </returns>
		public override string ToString()
		{
			string recordData = RecordDataToString();
			return Name + " " + TimeToLive + " " + RecordClass.ToShortString() + " " + RecordType.ToShortString() + (String.IsNullOrEmpty(recordData) ? "" : " " + recordData);
		}
		#endregion

		#region Parsing
		internal abstract void ParseRecordData(byte[] resultData, int startPosition, int length);

		internal abstract void ParseRecordData(DomainName origin, string[] stringRepresentation);

		internal void ParseUnknownRecordData(string[] stringRepresentation)
		{
			if (stringRepresentation.Length < 2)
				throw new FormatException();

			if (stringRepresentation[0] != @"\#")
				throw new FormatException();

			int length = Int32.Parse(stringRepresentation[1]);

			byte[] byteData = String.Join("", stringRepresentation.Skip(2)).FromBase16String();

			if (length != byteData.Length)
				throw new FormatException();

			ParseRecordData(byteData, 0, length);
		}

		protected DomainName ParseDomainName(DomainName origin, string name)
		{
			if (String.IsNullOrEmpty(name))
				throw new ArgumentException("Name must be provided", nameof(name));

			if (name.EndsWith("."))
				return DomainName.ParseFromMasterfile(name);

			return DomainName.ParseFromMasterfile(name) + origin;
		}
		#endregion

		#region Encoding
		internal override sealed int MaximumLength => Name.MaximumRecordDataLength + 12 + MaximumRecordDataLength;

		internal void Encode(byte[] messageData, int offset, ref int currentPosition, Dictionary<DomainName, ushort> domainNames, bool useCanonical = false)
		{
			EncodeRecordHeader(messageData, offset, ref currentPosition, domainNames, useCanonical);
			EncodeRecordBody(messageData, offset, ref currentPosition, domainNames, useCanonical);
		}

		internal void EncodeRecordHeader(byte[] messageData, int offset, ref int currentPosition, Dictionary<DomainName, ushort> domainNames, bool useCanonical)
		{
			DnsMessageBase.EncodeDomainName(messageData, offset, ref currentPosition, Name, domainNames, useCanonical);
			DnsMessageBase.EncodeUShort(messageData, ref currentPosition, (ushort) RecordType);
			DnsMessageBase.EncodeUShort(messageData, ref currentPosition, (ushort) RecordClass);
			DnsMessageBase.EncodeInt(messageData, ref currentPosition, TimeToLive);
		}

		internal void EncodeRecordBody(byte[] messageData, int offset, ref int currentPosition, Dictionary<DomainName, ushort> domainNames, bool useCanonical)
		{
			int recordDataOffset = currentPosition + 2;
			EncodeRecordData(messageData, offset, ref recordDataOffset, domainNames, useCanonical);
			EncodeRecordLength(messageData, offset, ref currentPosition, domainNames, recordDataOffset);
		}

		internal void EncodeRecordLength(byte[] messageData, int offset, ref int recordDataOffset, Dictionary<DomainName, ushort> domainNames, int recordPosition)
		{
			DnsMessageBase.EncodeUShort(messageData, ref recordDataOffset, (ushort) (recordPosition - recordDataOffset - 2));
			recordDataOffset = recordPosition;
		}


		protected internal abstract int MaximumRecordDataLength { get; }

		protected internal abstract void EncodeRecordData(byte[] messageData, int offset, ref int currentPosition, Dictionary<DomainName, ushort> domainNames, bool useCanonical);
		#endregion

		internal T Clone<T>()
			where T : DnsRecordBase
		{
			return (T) MemberwiseClone();
		}

		public int CompareTo(DnsRecordBase other)
		{
			int compare = Name.CompareTo(other.Name);
			if (compare != 0)
				return compare;

			compare = RecordType.CompareTo(other.RecordType);
			if (compare != 0)
				return compare;

			compare = RecordClass.CompareTo(other.RecordClass);
			if (compare != 0)
				return compare;

			compare = TimeToLive.CompareTo(other.TimeToLive);
			if (compare != 0)
				return compare;

			byte[] thisBuffer = new byte[MaximumRecordDataLength];
			int thisLength = 0;
			EncodeRecordData(thisBuffer, 0, ref thisLength, null, false);

			byte[] otherBuffer = new byte[other.MaximumRecordDataLength];
			int otherLength = 0;
			other.EncodeRecordData(otherBuffer, 0, ref otherLength, null, false);

			for (int i = 0; i < Math.Min(thisLength, otherLength); i++)
			{
				compare = thisBuffer[i].CompareTo(otherBuffer[i]);
				if (compare != 0)
					return compare;
			}

			return thisLength.CompareTo(otherLength);
		}

		private int? _hashCode;

		[SuppressMessage("ReSharper", "NonReadonlyMemberInGetHashCode")]
		public override int GetHashCode()
		{
			if (!_hashCode.HasValue)
			{
				_hashCode = ToString().GetHashCode();
			}

			return _hashCode.Value;
		}

		public override bool Equals(object obj)
		{
			return Equals(obj as DnsRecordBase);
		}

		public bool Equals(DnsRecordBase other)
		{
			if (other == null)
				return false;

			return base.Equals(other)
			       && RecordDataToString().Equals(other.RecordDataToString());
		}
	}
}