﻿using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Nevar
{
	public unsafe class TreeDumper
	{
		public static void Dump(Transaction tx, string path, Page start, int showNodesEvery = 25)
		{
			using (var writer = File.CreateText(path))
			{
				writer.WriteLine(@"
digraph structs {
    node [shape=Mrecord]
    rankdir=LR;
	bgcolor=transparent;
");

				var stack = new Stack<Page>();
				stack.Push(start);
				var references = new StringBuilder();
				while (stack.Count > 0)
				{
					var p = stack.Pop();

					writer.WriteLine(@"
	subgraph cluster_p_{0} {{ 
		label=""Page #{0}"";
		color={4};
		p_{0} [label=""Page: {0}|{1}|Entries: {2:#,#}|Avl Space: {3:#,#}""];

", p.PageNumber, p.Flags, p.NumberOfEntries, p.SizeLeft, p.IsLeaf ? "black" : "blue");
					var key = new Slice(SliceOptions.Key);
					if (p.IsLeaf && showNodesEvery > 0)
					{
						writer.WriteLine("		p_{0}_nodes [label=\" Entries:", p.PageNumber);
						for (int i = 0; i < p.NumberOfEntries; i += showNodesEvery)
						{
							if (i != 0 && showNodesEvery >= 5)
							{
								writer.WriteLine(" ... {0:#,#} keys redacted ...", showNodesEvery);
							}
							var node = p.GetNode(i);
							key.Set(node);
							writer.WriteLine("{0} - Size {1:#,#} {2}", key, node->DataSize, node->Flags == NodeFlags.None ? "" : node->Flags.ToString());
						}
						writer.WriteLine("\"];");
					}
					else if (p.IsBranch)
					{
						writer.Write("		p_{0}_refs [label=\"", p.PageNumber);
						for (int i = 0; i < p.NumberOfEntries; i++)
						{
							var node = p.GetNode(i);

							writer.Write("{3}<{2}> {0}  / to page {1}", GetBranchNodeString(i, key, p, node), node->PageNumber,
								i, i == 0 ? "" : "|");
						}
						writer.WriteLine("\"];");
						var prev = -1;
						for (int i = 0; i < p.NumberOfEntries; i++)
						{
							var node = p.GetNode(i);
							var child = tx.GetPage(node->PageNumber);
							stack.Push(child);

							references.AppendFormat("	p_{0}_refs:{3} -> p_{1} [label=\"{2}\"];", p.PageNumber, child.PageNumber, GetBranchNodeString(i, key, p, node), i).AppendLine();
							if (prev > -1)
								references.AppendFormat("	p_{0} -> p_{1} [style=\"invis\"];", child.PageNumber, prev);

							prev = child.PageNumber;
						}
					}
					writer.WriteLine("	}");
				}
				writer.WriteLine(references.ToString());

				writer.WriteLine("}");
			}
		}

		private static unsafe string GetBranchNodeString(int i, Slice key, Page p, NodeHeader* node)
		{
			string keyStr;
			if (i == 0)
			{
				key.Set(p.GetNode(1));
				keyStr = "(lt " + key + ")";
			}
			else
			{
				key.Set(node);
				keyStr = key.ToString();
			}
			return keyStr;
		}
	}
}