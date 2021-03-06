﻿using System;
using System.IO;

namespace Voron.Trees
{
    public unsafe class EmptyIterator : IIterator
    {
        public bool Seek(Slice key)
        {
            return false;
        }

        public Slice CurrentKey
        {
            get { return new Slice(Current); }
        }

        public  int GetCurrentDataSize()
        {
            throw new InvalidOperationException("No current page");
        }

        public Stream CreateStreamForCurrent()
        {
            throw new InvalidOperationException("No current page");
        }

        public unsafe NodeHeader* Current
        {
            get
            {
               throw new InvalidOperationException("No current page");
            }
        }

        public Slice MaxKey { get; set; }

        public Slice RequiredPrefix
        {
            get;
            set;
        }

        public bool MoveNext()
        {
            return false;
        }

        public void Dispose()
        {
        }
    }
}