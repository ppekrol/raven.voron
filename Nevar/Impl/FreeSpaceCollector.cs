﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Nevar.Trees;

namespace Nevar.Impl
{
    public class FreeSpaceCollector
    {
        private readonly StorageEnvironment _env;
        private long _freeSpaceGatheredMinTx;
        private Slice _freeSpaceKey;
        private int _originalFreeSpaceCount;
        private bool _alreadyLookingForFreeSpace;
        private readonly ConsecutiveSequences _freeSpace = new ConsecutiveSequences();
        private int _lastTransactionPageUsage;

        public FreeSpaceCollector(StorageEnvironment env)
        {
            _env = env;
        }

        public Page TryAllocateFromFreeSpace(Transaction tx, int num)
        {
            if (_env.FreeSpace == null)
                return null;// this can happen the first time FreeSpace tree is created

            if (_alreadyLookingForFreeSpace)
                return null;// can't recursively find free space

            _alreadyLookingForFreeSpace = true;
            try
            {
                while (true)
                {
                    long page;
                    if (_freeSpace.TryAllocate(num, out page))
                    {
                        var newPage = tx.Pager.Get(tx, page);
                        newPage.PageNumber = page;
                        return newPage;
                    }

                    if (_freeSpaceGatheredMinTx >= tx.Id)
                        return null;
                    GatherFreeSpace(tx);
                }
            }
            finally
            {
                _alreadyLookingForFreeSpace = false;
            }
        }


        public void LastTransactionPageUsage(int pages)
        {
            if (pages == _lastTransactionPageUsage)
                return;

            // if there is a difference, we apply 1/4 the difference to the current value
            // this is to make sure that we don't suddenly spike the required pages per transaction
            // just because of one abnormally large / small transaction
            _lastTransactionPageUsage += (pages - _lastTransactionPageUsage) / 4;
        }


        public void SaveOldFreeSpace(Transaction tx)
        {
            if (_freeSpace.Count == _originalFreeSpaceCount)
                return;
            Debug.Assert(_freeSpaceKey != null);

            foreach (var slice in GetOldTransactionsToDelete(tx))
            {
                _env.FreeSpace.Delete(tx, slice);
            }


            using (var ms = new MemoryStream())
            using (var writer = new BinaryWriter(ms))
            {
                foreach (var i in _freeSpace)
                {
                    writer.Write(i);
                }
                ms.Position = 0;
                _env.FreeSpace.Add(tx, _freeSpaceKey, ms);
            }
        }

        private unsafe IEnumerable<Slice> GetOldTransactionsToDelete(Transaction tx)
        {
            Debug.Assert(_freeSpaceKey != null);
            
            var toDelete = new List<Slice>();

            using (var it = _env.FreeSpace.Iterate(tx))
            {
                if (it.Seek(Slice.BeforeAllKeys) == false)
                    return toDelete;

                do
                {
                    var slice = new Slice(it.Current);

                    if (slice.Compare(_freeSpaceKey, _env.SliceComparer) > 0)
                        break;

                    toDelete.Add(slice);
                } while (it.MoveNext());
            }
            return toDelete;
        }

        /// <summary>
        /// This method will find all the currently free space in the database and make it easily available 
        /// for the transaction. This has to be called _after_ the transaction has already been setup.
        /// </summary>
        private unsafe void GatherFreeSpace(Transaction tx)
        {
            if (tx.Flags.HasFlag(TransactionFlags.ReadWrite) == false)
                throw new InvalidOperationException("Cannot gather free space in a read only transaction");
            if (_freeSpaceGatheredMinTx >= tx.Id)
                return;

            _freeSpaceGatheredMinTx = tx.Id;
            if (_env.FreeSpace == null)
                return;

            var oldestTx = _env.OldestTransaction;

            var toDelete = new List<Slice>();
            using (var iterator = _env.FreeSpace.Iterate(tx))
            {
                if (iterator.Seek(_freeSpaceKey ?? Slice.BeforeAllKeys) == false)
                    return;

#if DEBUG
                var additions = new HashSet<long>();
#endif
                do
                {
                    var node = iterator.Current;
                    var slice = new Slice(node);

                    if (_freeSpaceKey != null &&
                        slice.Compare(_freeSpaceKey, _env.SliceComparer) <= 0) // we already have this in memory, so we can just skip it
                        continue;

                    var txId = slice.ToInt64() >> 8;

                    if (oldestTx != 0 && txId >= oldestTx)
                        break;  // all the free space after this is tied up in active transactions

                    toDelete.Add(slice);
                    var remainingPages = tx.GetNumberOfFreePages(node);

                    using (var data = Tree.StreamForNode(tx, node))
                    using (var reader = new BinaryReader(data))
                    {
                        for (int i = 0; i < remainingPages; i++)
                        {
                            var pageNum = reader.ReadInt64();
#if DEBUG
                            Debug.Assert(pageNum >= 2 && pageNum <= tx.Pager.NumberOfAllocatedPages);
                            var condition = additions.Add(pageNum);
                            Debug.Assert(condition); // free page number HAVE to be unique
#endif
                            _freeSpace.Add(pageNum);
                        }
                    }

                } while (iterator.MoveNext());
            }

            if (toDelete.Count == 0)
                return;

            _freeSpaceKey = toDelete[toDelete.Count - 1].Clone(); // this is the latest available
            // we record the current amount we have, so we know if we need to skip past it
            _originalFreeSpaceCount = _freeSpace.Count;
        }
    }
}