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
using PdfClown.Files;
using PdfClown.Tokens;

using System;

namespace PdfClown.Objects
{
    /**
      <summary>PDF indirect reference object [PDF:1.6:3.2.9].</summary>
    */
    public sealed class PdfReference : PdfDirectObject, IPdfIndirectObject
    {
        #region static
        private const int DelegatedReferenceNumber = -1;
        #endregion

        #region dynamic
        #region fields
        private readonly int generationNumber;
        private readonly int objectNumber;

        private PdfIndirectObject indirectObject;

        private File file;
        private PdfObject parent;
        private bool updated;
        private string id;
        #endregion

        #region constructors
        internal PdfReference(PdfIndirectObject indirectObject)
        {
            this.objectNumber = DelegatedReferenceNumber;
            this.generationNumber = DelegatedReferenceNumber;

            this.indirectObject = indirectObject;
        }

        internal PdfReference(int objectNumber, int generationNumber, File file)
        {
            this.objectNumber = objectNumber;
            this.generationNumber = generationNumber;

            this.file = file;
        }
        #endregion

        #region interface
        #region public
        public override File File => file ?? base.File;

        /**
          <summary>Gets the generation number.</summary>
        */
        public int GenerationNumber => generationNumber == DelegatedReferenceNumber ? IndirectObject.XrefEntry.Generation : generationNumber;

        /**
          <summary>Gets the object identifier.</summary>
          <remarks>This corresponds to the serialized representation of an object identifier within a PDF file.</remarks>
        */
        public string Id => id ??= $"{ObjectNumber}{Symbol.Space}{GenerationNumber}";

        /**
          <summary>Gets the object reference.</summary>
          <remarks>This corresponds to the serialized representation of a reference within a PDF file.</remarks>
        */
        public string IndirectReference => ($"{Id}{Symbol.Space}{Symbol.CapitalR}");

        /**
          <summary>Gets the object number.</summary>
        */
        public int ObjectNumber => objectNumber == DelegatedReferenceNumber ? IndirectObject.XrefEntry.Number : objectNumber;

        public override PdfObject Parent
        {
            get => parent;
            internal set => parent = value;
        }

        public override bool Updateable
        {
            get => IndirectObject?.Updateable ?? false;
            set =>
                /*
NOTE: Fail fast if the referenced indirect object is undefined.
*/
                IndirectObject.Updateable = value;
        }

        public override bool Updated
        {
            get => updated;
            protected internal set => updated = value;
        }


        #region IPdfIndirectObject
        public PdfDataObject DataObject
        {
            get => IndirectObject?.DataObject;
            set =>
                /*
NOTE: Fail fast if the referenced indirect object is undefined.
*/
                IndirectObject.DataObject = value;
        }

        /**
          <returns><code>null</code>, if the indirect object is undefined.</returns>
        */
        public override PdfIndirectObject IndirectObject => indirectObject ??= file.IndirectObjects[objectNumber];

        public override PdfReference Reference => this;
        #endregion
        #endregion

        #region protected
        protected internal override bool Virtual
        {
            get => IndirectObject?.Virtual ?? false;
            set =>
                /*
NOTE: Fail fast if the referenced indirect object is undefined.
*/
                IndirectObject.Virtual = value;
        }

        public override PdfObject Swap(PdfObject other)
        {
            /*
              NOTE: Fail fast if the referenced indirect object is undefined.
            */
            return IndirectObject.Swap(((PdfReference)other).IndirectObject).Reference;
        }

        public override int GetHashCode()
        {
            /*
              NOTE: Uniqueness should be achieved XORring the (local) reference hash-code with the (global)
              file hash-code.
            */
            return Id.GetHashCode() ^ File.GetHashCode();
        }

        public override string ToString()
        {
            return IndirectReference;
        }

        public override void WriteTo(IOutputStream stream, File context)
        {
            stream.Write(IndirectReference);
        }

        public override PdfObject Accept(IVisitor visitor, object data)
        {
            return visitor.Visit(this, data);
        }

        public override PdfDataObject Resolve()
        {
            return DataObject;
        }

        public override int CompareTo(PdfDirectObject obj)
        {
            if (obj == null)
                return 1;
            if (ReferenceEquals(this, obj))
                return 0;
            if (obj is PdfReference reference)
                return string.Compare(Id, reference.Id, StringComparison.Ordinal);
            else
                return GetHashCode().CompareTo(obj.GetHashCode());
        }

        public override bool Equals(object other)
        {
            /*
             * NOTE: References are evaluated as "equal" if they are either the same instance or they sport
             * the same identifier within the same file instance.
             */
            if (base.Equals(other))
                return true;
            else if (other == null
                || !other.GetType().Equals(GetType()))
                return false;

            PdfReference otherReference = (PdfReference)other;
            return otherReference.File == File
                && otherReference.Id.Equals(Id, StringComparison.Ordinal);
        }


        #endregion
        #endregion
        #endregion
    }
}