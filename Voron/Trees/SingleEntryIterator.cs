﻿using System;
using System.IO;
using Voron.Impl;

namespace Voron.Trees
{
    public unsafe class SingleEntryIterator : IIterator
    {
        private readonly SliceComparer _cmp;
        private readonly NodeHeader* _item;
        private readonly Transaction _tx;

        public SingleEntryIterator(SliceComparer cmp, NodeHeader* item, Transaction tx)
        {
            _cmp = cmp;
            _item = item;
            _tx = tx;
        }

        public int GetCurrentDataSize()
        {
            throw new NotSupportedException("There is no value for single entry iterator");
        }

        public Slice CurrentKey
        {
            get; private set;
        }

        public bool Seek(Slice key)
        {
            if (this.ValidateCurrentKey(Current, _cmp) == false)
                return false;
            CurrentKey = NodeHeader.GetData(_tx, _item);
            return true;
        }

        public NodeHeader* Current
        {
            get { return _item; }
        }

        public Slice RequiredPrefix
        {
            get; set;
        }

        public Slice MaxKey
        {
            get; set;
        }

        public bool MoveNext()
        {
            CurrentKey = null;
            return false;
        }

        public Stream CreateStreamForCurrent()
        {
            throw new NotSupportedException("There is no value for single entry iterator");
        }

        public void Dispose()
        {
        }
    }
}
