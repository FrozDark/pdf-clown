﻿/*
  Copyright 2006-2011 Stefano Chizzolini. http://www.pdfclown.org

  Contributors:
    * Alexandr Vassilyev (alexandr_vslv@mail.ru)

  This file should be part of the source code distribution of "PDF Clown library" (the
  Program): see the accompanying README files for more info.

  This Program is free software; you can redistribute it and/or modify it under the terms
  of the GNU Lesser General Public License as published by the Free Software Foundation;
  either version 3 of the License, or (at your option) any later version.

  This Program is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY,
  either expressed or implied; without even the implied warranty of MERCHANTABILITY or
  FITNESS FOR A PARTICULAR PURPOSE. See the License for more details.

  You should have received a copy of the GNU Lesser General Public License along with this
  Program (see README files); if not, go to the GNU website (http://www.gnu.org/licenses/).

  Redistribution and use, with or without modification, are permitted provided that such
  redistributions retain the above copyright notice, license and disclaimer, along with
  this list of conditions.
*/

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using org.pdfclown.bytes;

namespace org.pdfclown.documents.contents.colorSpaces
{
    public class ICCProfile
    {
        public static readonly Dictionary<uint, Type> Types = new Dictionary<uint, Type>()
        {
            { ICCChromaticityType.chrm, typeof(ICCChromaticityType) },
            { ICCCrdInfoType.crdi, typeof(ICCCrdInfoType) },
            { ICCCurveType.curv, typeof(ICCCurveType) },
            { ICCDataType.data, typeof(ICCDataType) },
            { ICCDateTimeType.dtim, typeof(ICCDateTimeType) },
            { ICCDeviceSettingsType.devs, typeof(ICCDeviceSettingsType) },
            { ICCLut16Type.mft2, typeof(ICCLut16Type) },
            { ICCLut8Type.mft1, typeof(ICCLut8Type) },
            { ICCMeasurementType.meas, typeof(ICCMeasurementType) },
            { ICCNamedColorType.ncol, typeof(ICCNamedColorType) },
            { ICCNamedColor2Type.ncl2, typeof(ICCNamedColor2Type)  },
            { ICCProfileSequenceDescType.pseq, typeof(ICCProfileSequenceDescType)  },
            { ICCResponseCurveSet16Type.rcs2, typeof(ICCResponseCurveSet16Type)  },
            { ICCS15Fixed16ArrayType.sf32, typeof(ICCS15Fixed16ArrayType)  },
            { ICCU16Fixed16ArrayType.uf32, typeof(ICCU16Fixed16ArrayType)  },
            { ICCUInt16ArrayType.ui16, typeof(ICCUInt16ArrayType)  },
            { ICCUInt32ArrayType.ui32, typeof(ICCUInt32ArrayType)  },
            { ICCUInt64ArrayType.ui64, typeof(ICCUInt64ArrayType)  },
            { ICCUInt8ArrayType.ui08, typeof(ICCUInt8ArrayType)  },
            { ICCScreeningType.scrn, typeof(ICCScreeningType)  },
            { ICCSignatureType.sig, typeof(ICCSignatureType)  },
            { ICCTextDescriptionType.desc, typeof(ICCTextDescriptionType)  },
            { ICCTextType.text, typeof(ICCTextType)  },
            { ICCUcrbgType.bfd, typeof(ICCUcrbgType)  },
            { ICCViewingConditionsTypee.view, typeof(ICCViewingConditionsTypee)  },
            { ICCXYZType.XYZ, typeof(ICCXYZType)  },

        };

        public static ICCProfile Load(byte[] data)
        {
            var profile = new ICCProfile();
            var header = new ICCHeader();
            var buffer = new bytes.Buffer(data);
            header.ProfileSize = buffer.ReadUnsignedInt();
            header.CMMTypeSignature = buffer.ReadUnsignedInt();
            header.ProfileVersionNumber.Major = (byte)buffer.ReadByte();
            header.ProfileVersionNumber.Minor = (byte)buffer.ReadByte();
            header.ProfileVersionNumber.Reserv1 = (byte)buffer.ReadByte();
            header.ProfileVersionNumber.Reserv2 = (byte)buffer.ReadByte();
            header.ProfileDeviceClassSignature = (ICCProfileDeviceSignatures)buffer.ReadUnsignedInt();
            header.ColorSpaceOfData = (ICCColorSpaceSignatures)buffer.ReadUnsignedInt();
            header.ProfileConnectionSpace = (ICCColorSpaceSignatures)buffer.ReadUnsignedInt();
            header.DateCreated.Load(buffer);
            header.acsp = buffer.ReadUnsignedInt();
            header.PrimaryPlatformSignature = (ICCPrimaryPlatformSignatures)buffer.ReadUnsignedInt();
            header.Flags = (ICCProfileFlags)buffer.ReadUnsignedInt();
            header.DeviceManufacturer = buffer.ReadUnsignedInt();
            header.DeviceModel = buffer.ReadUnsignedInt();
            header.DeviceAttributes.Load(buffer);
            header.RenderingIntent.Intents = buffer.ReadUnsignedShort();
            header.RenderingIntent.Reserved = buffer.ReadUnsignedShort();
            header.XYZ.Load(buffer);
            header.ProfileCreatorSignature = buffer.ReadUnsignedInt();
            header.FutureUse = new byte[44];
            buffer.Read(header.FutureUse);
            profile.Header = header;
            var tagCount = buffer.ReadUnsignedInt();
            for (int i = 0; i < tagCount; i++)
            {
                var tag = new ICCTagTable();
                tag.Signature = (ICCTagTypes)buffer.ReadUnsignedInt();
                tag.Offset = buffer.ReadUnsignedInt();
                tag.ElementSize = buffer.ReadUnsignedInt();
                profile.Tags.Add(tag);
            }
            foreach (var tagTable in profile.Tags)
            {
                buffer.Seek(tagTable.Offset);
                var key = buffer.ReadUnsignedInt();
                if (Types.TryGetValue(key, out var type))
                {
                    tagTable.Tag = (ICCTag)Activator.CreateInstance(type, tagTable);
                    tagTable.Tag.Profile = profile;
                    tagTable.Tag.Load(buffer);
                }
            }
            return profile;
        }

        //https://stackoverflow.com/a/2887/4682355
        public static T ByteArrayToStructure<T>(byte[] bytes) where T : struct
        {
            GCHandle handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
            try
            {
                return Marshal.PtrToStructure<T>(handle.AddrOfPinnedObject());
            }
            finally
            {
                handle.Free();
            }

        }

        public ICCHeader Header;

        public List<ICCTagTable> Tags { get; set; } = new List<ICCTagTable>();
    }
}