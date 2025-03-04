/*
  Copyright 2006-2015 Stefano Chizzolini. http://www.pdfclown.org

  Contributors:
    * Stefano Chizzolini (original code developer, http://www.stefanochizzolini.it)

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

using PdfClown.Bytes;
using PdfClown.Documents;
using PdfClown.Files;
using PdfClown.Objects;
using PdfClown.Util.Collections.Generic;
using PdfClown.Util.Parsers;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace PdfClown.Tokens
{
    /**
      <summary>PDF file reader.</summary>
    */
    public sealed class Reader : IDisposable
    {
        #region types
        public sealed class FileInfo
        {
            private PdfDictionary trailer;
            private Version version;
            private SortedDictionary<int, XRefEntry> xrefEntries;

            internal FileInfo(Version version, PdfDictionary trailer, SortedDictionary<int, XRefEntry> xrefEntries)
            {
                this.version = version;
                this.trailer = trailer;
                this.xrefEntries = xrefEntries;
            }

            public PdfDictionary Trailer => trailer;

            public Version Version => version;

            public SortedDictionary<int, XRefEntry> XrefEntries => xrefEntries;
        }
        #endregion

        #region dynamic
        #region fields
        private FileParser parser;
        #endregion

        #region constructors
        internal Reader(IInputStream stream, Files.File file, string password = null, System.IO.Stream keyStoreInputStream = null)
        { this.parser = new FileParser(stream, file, password, keyStoreInputStream); }

        ~Reader()
        { Dispose(false); }
        #endregion

        #region interface
        #region public
        public override int GetHashCode()
        {
            return parser.GetHashCode();
        }

        public FileParser Parser => parser;

        /**
          <summary>Retrieves the file information.</summary>
        */
        public FileInfo ReadInfo()
        {
            //TODO:hybrid xref table/stream
            Version version = Version.Get(parser.RetrieveVersion());
            PdfDictionary trailer = null;
            SortedDictionary<int, XRefEntry> xrefEntries = new SortedDictionary<int, XRefEntry>();
            {
                long sectionOffset = parser.RetrieveXRefOffset();
                while (sectionOffset > -1)
                {
                    // Move to the start of the xref section!
                    parser.Seek(sectionOffset);

                    PdfDictionary sectionTrailer;
                    if (string.Equals(parser.GetToken(1)?.ToString(), Keyword.XRef, StringComparison.Ordinal)) // XRef-table section.
                    {
                        // Looping sequentially across the subsections inside the current xref-table section...
                        while (true)
                        {
                            /*
                              NOTE: Each iteration of this block represents the scanning of one subsection.
                              We get its bounds (first and last object numbers within its range) and then collect
                              its entries.
                            */
                            // 1. First object number.
                            parser.MoveNext();
                            if ((parser.TokenType == PostScriptParser.TokenTypeEnum.Keyword)
                                && string.Equals(parser.Token.ToString(), Keyword.Trailer, StringComparison.Ordinal)) // XRef-table section ended.
                                break;
                            else if (parser.TokenType != PostScriptParser.TokenTypeEnum.Integer)
                                throw new PostScriptParseException("Neither object number of the first object in this xref subsection nor end of xref section found.", parser);

                            // Get the object number of the first object in this xref-table subsection!
                            int startObjectNumber = (int)parser.Token;

                            // 2. Last object number.
                            parser.MoveNext();
                            if (parser.TokenType != PostScriptParser.TokenTypeEnum.Integer)
                                throw new PostScriptParseException("Number of entries in this xref subsection not found.", parser);

                            // Get the object number of the last object in this xref-table subsection!
                            int endObjectNumber = (int)parser.Token + startObjectNumber;

                            // 3. XRef-table subsection entries.
                            for (int index = startObjectNumber; index < endObjectNumber; index++)
                            {
                                if (xrefEntries.ContainsKey(index)) // Already-defined entry.
                                {
                                    // Skip to the next entry!
                                    parser.MoveNext(3);
                                    continue;
                                }

                                // Get the indirect object offset!
                                int offset = (int)parser.GetToken(1);
                                // Get the object generation number!
                                int generation = (int)parser.GetToken(1);
                                // Get the usage tag!
                                XRefEntry.UsageEnum usage;
                                {
                                    string usageToken = (string)parser.GetToken(1);
                                    if (string.Equals(usageToken, Keyword.InUseXrefEntry, StringComparison.Ordinal))
                                        usage = XRefEntry.UsageEnum.InUse;
                                    else if (string.Equals(usageToken, Keyword.FreeXrefEntry, StringComparison.Ordinal))
                                        usage = XRefEntry.UsageEnum.Free;
                                    else
                                        throw new PostScriptParseException("Invalid xref entry.", parser);
                                }

                                // Define entry!
                                xrefEntries[index] = new XRefEntry(index, generation, offset, usage);
                            }
                        }

                        // Get the previous trailer!
                        sectionTrailer = (PdfDictionary)parser.ParsePdfObject(1);
                    }
                    else // XRef-stream section.
                    {
                        //skipping the indirect - object header.
                        XRefStream stream = (XRefStream)parser.ParsePdfObject(1); // Gets the xref stream 
                                                                                  // XRef-stream subsection entries.
                        foreach (XRefEntry xrefEntry in stream.Values)
                        {
                            if (xrefEntries.ContainsKey(xrefEntry.Number)) // Already-defined entry.
                                continue;

                            // Define entry!
                            xrefEntries[xrefEntry.Number] = xrefEntry;
                        }

                        // Get the previous trailer!
                        sectionTrailer = stream.Header;
                    }

                    if (trailer == null)
                    { trailer = sectionTrailer; }

                    // Get the previous xref-table section's offset!
                    sectionOffset = sectionTrailer.GetInt(PdfName.Prev, -1);
                }
            }

            return new FileInfo(version, trailer, xrefEntries);
        }

        internal void PrepareDecryption()
        {
            parser.PrepareDecryption();
        }

        #region IDisposable
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        #endregion
        #endregion

        #region private
        private void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (parser != null)
                {
                    parser.Dispose();
                    parser = null;
                }
            }
        }
        #endregion
        #endregion
        #endregion
    }
}