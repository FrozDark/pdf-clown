/*
  Copyright 2006-2013 Stefano Chizzolini. http://www.pdfclown.org

  Contributors:
    * Stefano Chizzolini (original code developer, http://www.stefanochizzolini.it):
      - porting and adaptation (extension to any bit depth other than 8) of [JT]
        predictor-decoding implementation.
    * Joshua Tauberer (code contributor, http://razor.occams.info):
      - predictor-decoding contributor on .NET implementation.
    * Jean-Claude Truy (bugfix contributor): [FIX:0.0.8:JCT].

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

using PdfClown.Objects;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;

namespace PdfClown.Bytes.Filters
{
    /**
      <summary>zlib/deflate [RFC:1950,1951] filter [PDF:1.6:3.3.3].</summary>
    */
    [PDF(VersionEnum.PDF12)]
    public class FlateFilter : Filter
    {
        #region dynamic
        #region constructors
        internal FlateFilter()
        { }
        #endregion

        #region interface
        #region public
        public override byte[] Decode(Bytes.Buffer data, PdfDirectObject parameters, IDictionary<PdfName, PdfDirectObject> header)
        {
            using (var outputStream = new MemoryStream())
            using (var inputStream = new MemoryStream(data.GetBuffer(), 0, (int)data.Length))
            using (var inputFilter = new ICSharpCode.SharpZipLib.Zip.Compression.Streams.InflaterInputStream(inputStream))
            {
                Transform(inputFilter, outputStream);
                inputFilter.Close();
                return DecodePredictor(outputStream.ToArray(), parameters, header);
            }
        }

        public override byte[] Encode(Bytes.Buffer data, PdfDirectObject parameters, IDictionary<PdfName, PdfDirectObject> header)
        {
            using (var inputStream = new MemoryStream(data.GetBuffer(), 0, (int)data.Length))
            using (var outputStream = new MemoryStream())
            using (var outputFilter = new ICSharpCode.SharpZipLib.Zip.Compression.Streams.DeflaterOutputStream(outputStream))
            {
                // Add zlib's 2-byte header [RFC 1950] [FIX:0.0.8:JCT]!
                //outputStream.WriteByte(0x78); // CMF = {CINFO (bits 7-4) = 7; CM (bits 3-0) = 8} = 0x78.
                //outputStream.WriteByte(0xDA); // FLG = {FLEVEL (bits 7-6) = 3; FDICT (bit 5) = 0; FCHECK (bits 4-0) = {31 - ((CMF * 256 + FLG - FCHECK) Mod 31)} = 26} = 0xDA.
                Transform(inputStream, outputFilter);
                outputFilter.Close();
                return outputStream.ToArray();
            }
        }
        #endregion

        #region private
        protected byte[] DecodePredictor(byte[] data, PdfDirectObject parameters, IDictionary<PdfName, PdfDirectObject> header)
        {
            if (!(parameters is PdfDictionary))
                return data;
            var dictionary = parameters as PdfDictionary;

            int predictor = dictionary.GetInt(PdfName.Predictor, 1);
            if (predictor == 1) // No predictor was applied during data encoding.
                return data;

            int sampleComponentBitsCount = (dictionary.TryGetValue(PdfName.BitsPerComponent, out var bintsPerComponent) ? ((PdfInteger)bintsPerComponent).RawValue : 8);
            int sampleComponentsCount = (dictionary.TryGetValue(PdfName.Colors, out var colors) ? ((PdfInteger)colors).RawValue : 1);
            int rowSamplesCount = (dictionary.TryGetValue(PdfName.Columns, out var columns) ? ((PdfInteger)columns).RawValue : 1);

            using (MemoryStream input = new MemoryStream(data))
            using (MemoryStream output = new MemoryStream())
            {
                switch (predictor)
                {
                    case 2: // TIFF Predictor 2 (component-based).
                        {
                            int[] sampleComponentPredictions = new int[sampleComponentsCount];
                            int sampleComponentDelta = 0;
                            int sampleComponentIndex = 0;
                            while ((sampleComponentDelta = input.ReadByte()) != -1)
                            {
                                int sampleComponent = sampleComponentDelta + sampleComponentPredictions[sampleComponentIndex];
                                output.WriteByte((byte)sampleComponent);

                                sampleComponentPredictions[sampleComponentIndex] = sampleComponent;

                                sampleComponentIndex = ++sampleComponentIndex % sampleComponentsCount;
                            }
                            break;
                        }
                    default: // PNG Predictors [RFC 2083] (byte-based).
                        {
                            int sampleBytesCount = (int)Math.Ceiling(sampleComponentBitsCount * sampleComponentsCount / 8d); // Number of bytes per pixel (bpp).
                            int rowSampleBytesCount = (int)Math.Ceiling(sampleComponentBitsCount * sampleComponentsCount * rowSamplesCount / 8d) + sampleBytesCount; // Number of bytes per row (comprising a leading upper-left sample (see Paeth method)).
                            int[] previousRowBytePredictions = new int[rowSampleBytesCount];
                            int[] currentRowBytePredictions = new int[rowSampleBytesCount];
                            int[] leftBytePredictions = new int[sampleBytesCount];
                            int predictionMethod;
                            while ((predictionMethod = input.ReadByte()) != -1)
                            {
                                Array.Copy(currentRowBytePredictions, 0, previousRowBytePredictions, 0, currentRowBytePredictions.Length);
                                Array.Clear(leftBytePredictions, 0, leftBytePredictions.Length);
                                for (
                                  int rowSampleByteIndex = sampleBytesCount; // Starts after the leading upper-left sample (see Paeth method).
                                  rowSampleByteIndex < rowSampleBytesCount;
                                  rowSampleByteIndex++
                                  )
                                {
                                    int byteDelta = input.ReadByte();

                                    int sampleByteIndex = rowSampleByteIndex % sampleBytesCount;

                                    int sampleByte;
                                    switch (predictionMethod)
                                    {
                                        case 0: // None (no prediction).
                                            sampleByte = byteDelta;
                                            break;
                                        case 1: // Sub (predicts the same as the sample to the left).
                                            sampleByte = byteDelta + leftBytePredictions[sampleByteIndex];
                                            break;
                                        case 2: // Up (predicts the same as the sample above).
                                            sampleByte = byteDelta + previousRowBytePredictions[rowSampleByteIndex];
                                            break;
                                        case 3: // Average (predicts the average of the sample to the left and the sample above).
                                            sampleByte = byteDelta + (int)Math.Floor(((leftBytePredictions[sampleByteIndex] + previousRowBytePredictions[rowSampleByteIndex])) / 2d);
                                            break;
                                        case 4: // Paeth (a nonlinear function of the sample above, the sample to the left, and the sample to the upper left).
                                            {
                                                int paethPrediction;
                                                {
                                                    int leftBytePrediction = leftBytePredictions[sampleByteIndex];
                                                    int topBytePrediction = previousRowBytePredictions[rowSampleByteIndex];
                                                    int topLeftBytePrediction = previousRowBytePredictions[rowSampleByteIndex - sampleBytesCount];
                                                    int initialPrediction = leftBytePrediction + topBytePrediction - topLeftBytePrediction;
                                                    int leftPrediction = Math.Abs(initialPrediction - leftBytePrediction);
                                                    int topPrediction = Math.Abs(initialPrediction - topBytePrediction);
                                                    int topLeftPrediction = Math.Abs(initialPrediction - topLeftBytePrediction);
                                                    if (leftPrediction <= topPrediction
                                                      && leftPrediction <= topLeftPrediction)
                                                    { paethPrediction = leftBytePrediction; }
                                                    else if (topPrediction <= topLeftPrediction)
                                                    { paethPrediction = topBytePrediction; }
                                                    else
                                                    { paethPrediction = topLeftBytePrediction; }
                                                }
                                                sampleByte = byteDelta + paethPrediction;
                                                break;
                                            }
                                        default:
                                            throw new NotSupportedException("Prediction method " + predictionMethod + " unknown.");
                                    }
                                    output.WriteByte((byte)sampleByte);

                                    leftBytePredictions[sampleByteIndex] = currentRowBytePredictions[rowSampleByteIndex] = (byte)sampleByte;
                                }
                            }
                            break;
                        }
                }
                return output.ToArray();
            }
        }

        private void Transform(System.IO.Stream input, System.IO.Stream output)
        {
            byte[] buffer = new byte[8192]; int bufferLength;
            while ((bufferLength = input.Read(buffer, 0, buffer.Length)) != 0)
            { output.Write(buffer, 0, bufferLength); }
            output.Flush();
        }
        #endregion
        #endregion
        #endregion
    }
}