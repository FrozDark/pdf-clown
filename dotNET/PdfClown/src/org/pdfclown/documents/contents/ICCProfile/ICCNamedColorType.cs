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

namespace org.pdfclown.documents.contents.colorSpaces
{
    public class ICCNamedColorType : ICCTag
    {
        public ICCNamedColorType(ICCTagTable table) : base(table)
        {
        }

        public const uint ncol = 0x6E636F6C;
        public uint Reserved = 0x00000000;
        public uint VendorSpecificFlag;
        public uint Count;
        public string Prefix;
        public string Suffix;
        public string FirstColor;

        public override void Load(bytes.Buffer buffer)
        {
            buffer.Seek(Table.Offset);
            buffer.ReadUnsignedInt();
            buffer.ReadUnsignedInt();
            VendorSpecificFlag = buffer.ReadUnsignedInt();
            Count = buffer.ReadUnsignedInt();
            Prefix = System.Text.Encoding.ASCII.GetString(buffer.ReadNullTermitaded());
            Suffix = System.Text.Encoding.ASCII.GetString(buffer.ReadNullTermitaded());
            FirstColor = System.Text.Encoding.ASCII.GetString(buffer.ReadNullTermitaded());
            //....color coordinates. Color space of data
        }
    }
}