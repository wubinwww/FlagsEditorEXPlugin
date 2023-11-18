﻿namespace FlagsEditorEXPlugin
{
    internal class FlagsGen9SV : FlagsOrganizer
    {
        static string? s_flagsList_res = null;

        const int Src_EventFlags = 0;
        const int Src_FieldItemFlags = 1;
        const int Src_HiddenItemFlags = 2;
        const int Src_TrainerFlags = 3;

        readonly uint[] HiddenItemsBlockKeys = new uint[]
        {
                // Paldea
                0x6DAB304B, // ~ South Province
                0x6EAB31DE, // ~ West Province
                0x6FAB3371, // ~ North Province
                0x6CAB2EB8, // ~ East Province

                // Area Zero
                0x9A7A41AB,
                0x9B7A433E,
                0x9C7A44D1,

                // DLC1
                0x917A3380,
                0xA07A4B1D,
        };

        protected override void InitFlagsData(SaveFile savFile, string? resData)
        {
            m_savFile = savFile;

#if DEBUG
            // Force refresh
            s_flagsList_res = null;
#endif

            s_flagsList_res = resData ?? s_flagsList_res ?? ReadResFile("flags_gen9sv.txt");

            int idxEventFlagsSection = s_flagsList_res.IndexOf("//\tEvent Flags");
            int idxFieldItemFlagsSection = s_flagsList_res.IndexOf("//\tField Item Flags");
            int idxHiddenItemsFlagsSection = s_flagsList_res.IndexOf("//\tHidden Item Flags");
            int idxTrainerFlagsSection = s_flagsList_res.IndexOf("//\tTrainer Flags");
            int idxEventWorkSection = s_flagsList_res.IndexOf("//\tEvent Work");

            AssembleList(s_flagsList_res[idxEventFlagsSection..], Src_EventFlags, "Event Flags", Array.Empty<bool>());
            AssembleList(s_flagsList_res[idxFieldItemFlagsSection..], Src_FieldItemFlags, "Field Item Flags", Array.Empty<bool>());
            AssembleList(s_flagsList_res[idxHiddenItemsFlagsSection..], Src_HiddenItemFlags, "Hidden Item Flags", Array.Empty<bool>());
            AssembleList(s_flagsList_res[idxTrainerFlagsSection..], Src_TrainerFlags, "Regular Trainer Flags", Array.Empty<bool>());

            AssembleWorkList(s_flagsList_res[idxEventWorkSection..], Array.Empty<uint>());
        }

        protected override void AssembleList(string flagsList_res, int sourceIdx, string sourceName, bool[] flagValues)
        {
            var savEventBlocks = ((ISCBlockArray)m_savFile!).Accessor;

            using (System.IO.StringReader reader = new System.IO.StringReader(flagsList_res))
            {
                FlagsGroup flagsGroup = new FlagsGroup(sourceIdx, sourceName);
                Dictionary<ulong, bool>? listOfStatuses = null;

                string? s = reader.ReadLine();

                if (s is null)
                {
                    return;
                }

                // Skip header
                if (s.StartsWith("//"))
                {
                    s = reader.ReadLine();
                }

                do
                {
                    if (!string.IsNullOrWhiteSpace(s))
                    {
                        // End of section
                        if (s.StartsWith("//"))
                        {
                            break;
                        }

                        switch (sourceIdx)
                        {
                            case Src_EventFlags:
                                {
                                    var flagDetail = new FlagDetail(s);
                                    if (savEventBlocks.HasBlock((uint)flagDetail.FlagIdx))
                                    {
                                        flagDetail.IsSet = (savEventBlocks.GetBlockSafe((uint)flagDetail.FlagIdx).Type == SCTypeCode.Bool2);
                                        flagDetail.SourceIdx = sourceIdx;
                                        flagsGroup.Flags.Add(flagDetail);
                                    }
                                }
                                break;

                            case Src_FieldItemFlags:
                                {
                                    if (listOfStatuses is null)
                                    {
                                        listOfStatuses = RetrieveBlockStatuses(savEventBlocks.GetBlockSafe(0x2482AD60).Data, emptyKey: 0x0000000000000000);
                                    }

                                    var flagDetail = new FlagDetail(s);
                                    if (listOfStatuses.TryGetValue(flagDetail.FlagIdx, out bool value))
                                    {
                                        flagDetail.IsSet = value;
                                        flagDetail.SourceIdx = sourceIdx;
                                        flagsGroup.Flags.Add(flagDetail);
                                    }
                                }
                                break;

                            case Src_HiddenItemFlags:
                                {
                                    if (listOfStatuses is null)
                                    {
                                        listOfStatuses = new Dictionary<ulong, bool>(10 * 1024);

                                        for (int k = 0; k < HiddenItemsBlockKeys.Length; k++)
                                        {
                                            // Skip DLC1 on older saves
                                            if (k > 6 && ((SAV9SV)m_savFile).SaveRevision < 1)
                                            {
                                                continue;
                                            }

                                            // Skip DLC2 on older saves
                                            if (k > 8 && ((SAV9SV)m_savFile).SaveRevision < 2)
                                            {
                                                continue;
                                            }

                                            var data = savEventBlocks.GetBlockSafe(HiddenItemsBlockKeys[k]).Data;
                                            int offsetAdjust = (int)k << 12;

                                            for (int i = 0; i < data.Length; i++)
                                            {
                                                listOfStatuses.Add((ulong)(i + offsetAdjust), data[i] == 0);
                                            }
                                        }

                                        /*StringBuilder sb = new StringBuilder();
                                        foreach (var _ in listOfStatuses)
                                        {
                                            sb.AppendFormat("0x{0:X4}\n", _.Key);
                                        }

                                        System.IO.File.WriteAllText("_idx.txt", sb.ToString());*/
                                    }

                                    var flagDetail = new FlagDetail(s);
                                    if (listOfStatuses.TryGetValue(flagDetail.FlagIdx, out bool value))
                                    {
                                        flagDetail.IsSet = value;
                                        flagDetail.SourceIdx = sourceIdx;
                                        flagsGroup.Flags.Add(flagDetail);
                                    }
                                }
                                break;

                            case Src_TrainerFlags:
                                {
                                    if (listOfStatuses is null)
                                    {
                                        listOfStatuses = new Dictionary<ulong, bool>(1000);

                                        // Trainer statuses tracker (base+)
                                        var trStatuses = RetrieveBlockStatuses(savEventBlocks.GetBlockSafe(0xF018C4AC).Data, emptyKey: 0xCBF29CE484222645);
                                        // Trainer statuses tracker (v2.0.2+)
                                        var trStatuses2 = RetrieveBlockStatuses(savEventBlocks.GetBlockSafe(0x28E475DE).Data, emptyKey: 0xCBF29CE484222645);

                                        foreach (var _ in trStatuses)
                                            listOfStatuses.Add(_.Key, _.Value);
                                        foreach (var _ in trStatuses2)
                                            listOfStatuses.Add(_.Key, _.Value);
                                    }

                                    var flagDetail = new FlagDetail(s);
                                    if (flagDetail.FlagTypeVal == EventFlagType.TrainerBattle)
                                    {
                                        if (listOfStatuses.TryGetValue(flagDetail.FlagIdx, out bool value))
                                        {
                                            flagDetail.IsSet = value;
                                        }

                                        flagDetail.SourceIdx = sourceIdx;
                                        flagsGroup.Flags.Add(flagDetail);
                                    }
                                }
                                break;
                        }
                    }

                    s = reader.ReadLine();

                } while (s != null);

                m_flagsGroupsList.Add(flagsGroup);
            }
        }

        protected override void AssembleWorkList<T>(string workList_res, T[] eventWorkValues)
        {
            var savEventBlocks = ((ISCBlockArray)m_savFile!).Accessor;

            using (System.IO.StringReader reader = new System.IO.StringReader(workList_res))
            {
                string? s = reader.ReadLine();

                if (s is null)
                {
                    return;
                }

                // Skip header
                if (s.StartsWith("//"))
                {
                    s = reader.ReadLine();
                }

                do
                {
                    if (!string.IsNullOrWhiteSpace(s))
                    {
                        // End of section
                        if (s.StartsWith("//"))
                        {
                            break;
                        }

                        var workDetail = new WorkDetail(s);
                        if (savEventBlocks.HasBlock((uint)workDetail.WorkIdx))
                        {
                            workDetail.Value = Convert.ToInt64(savEventBlocks.GetBlockSafe((uint)workDetail.WorkIdx).GetValue());
                            m_eventWorkList.Add(workDetail);
                        }
                    }

                    s = reader.ReadLine();

                } while (s != null);
            }
        }

        Dictionary<ulong, bool> RetrieveBlockStatuses(byte[] aData, ulong emptyKey)
        {
            // Ignore dummy blocks
            if (aData.Length == 0)
            {
                return new Dictionary<ulong, bool>();
            }

            var blocksStatus = new Dictionary<ulong, bool>(4000);

            using (var ms = new System.IO.MemoryStream(aData))
            {
                using (var reader = new System.IO.BinaryReader(ms))
                {
                    do
                    {
                        var key = reader.ReadUInt64();

                        if (key == emptyKey)
                        {
                            reader.ReadUInt64();
                            continue;
                        }

                        if (!blocksStatus.ContainsKey(key))
                        {
                            blocksStatus.Add(key, reader.ReadUInt64() == 1);
                        }
                        else
                        {
                            throw new ArgumentException("AHTB collision: 0x" + key.ToString("X8"));
                        }

                    } while (ms.Position < ms.Length);
                }
            }

            return blocksStatus;
        }

        public override bool SupportsBulkEditingFlags(EventFlagType flagType) => flagType switch
        {
            EventFlagType.FieldItem or
            EventFlagType.TrainerBattle or
            EventFlagType.FlySpot
                => true,
#if DEBUG
            EventFlagType.HiddenItem or
            EventFlagType.ItemGift or
            EventFlagType.PkmnGift or
            EventFlagType.StaticEncounter or
            EventFlagType.InGameTrade or
            EventFlagType.SideEvent
                => true,
#endif
            _ => false
        };

        public override void BulkMarkFlags(EventFlagType flagType)
        {
            ChangeFlagsVal(flagType, value: true);
        }

        public override void BulkUnmarkFlags(EventFlagType flagType)
        {
            ChangeFlagsVal(flagType, value: false);
        }

        void ChangeFlagsVal(EventFlagType flagType, bool value)
        {
            if (SupportsBulkEditingFlags(flagType))
            {
                //var savEventBlocks = ((ISCBlockArray)m_savFile!).Accessor;
                //byte[] bdata;

                switch (flagType)
                {
                    case EventFlagType.FieldItem:
                        {
                            foreach (var f in m_flagsGroupsList[Src_FieldItemFlags].Flags)
                            {
                                if (f.FlagTypeVal == flagType)
                                {
                                    f.IsSet = value;
                                }
                            }

                            SyncEditedFlags(Src_FieldItemFlags);
                        }
                        break;

                    case EventFlagType.HiddenItem:
                        {
                            foreach (var f in m_flagsGroupsList[Src_HiddenItemFlags].Flags)
                            {
                                if (f.FlagTypeVal == flagType)
                                {
                                    f.IsSet = value;
                                }
                            }

                            SyncEditedFlags(Src_HiddenItemFlags);
                        }
                        break;

                    case EventFlagType.TrainerBattle:
                        {
                            foreach (var f in m_flagsGroupsList[Src_TrainerFlags].Flags)
                            {
                                if (f.FlagTypeVal == flagType)
                                {
                                    f.IsSet = value;
                                }
                            }

                            SyncEditedFlags(Src_TrainerFlags);
                        }
                        break;

                    case EventFlagType.ItemGift:
                    case EventFlagType.PkmnGift:
                    case EventFlagType.StaticEncounter:
                    case EventFlagType.InGameTrade:
                    case EventFlagType.SideEvent:
                    case EventFlagType.FlySpot:
                        {
                            foreach (var f in m_flagsGroupsList[Src_EventFlags].Flags)
                            {
                                if (f.FlagTypeVal == flagType)
                                {
                                    f.IsSet = value;
                                }
                            }

                            SyncEditedFlags(Src_EventFlags);
                        }
                        break;
                }
            }
        }

        public override void SyncEditedFlags(int sourceIdx)
        {
            var savEventBlocks = ((ISCBlockArray)m_savFile!).Accessor;

            foreach (var fGroup in m_flagsGroupsList)
            {
                if (fGroup.SourceIdx == sourceIdx)
                {
                    switch (fGroup.SourceIdx)
                    {
                        case Src_EventFlags:
                            foreach (var f in fGroup.Flags)
                            {
                                savEventBlocks.GetBlockSafe((uint)f.FlagIdx).ChangeBooleanType(f.IsSet ? SCTypeCode.Bool2 : SCTypeCode.Bool1);
                            }
                            break;

                        case Src_FieldItemFlags:
                            {
                                var bdata = savEventBlocks.GetBlockSafe(0x2482AD60).Data;
                                using (var ms = new System.IO.MemoryStream(bdata))
                                {
                                    using (var writer = new System.IO.BinaryWriter(ms))
                                    {
                                        foreach (var f in fGroup.Flags)
                                        {
                                            if (ms.Position < ms.Length)
                                            {
                                                writer.Write(f.FlagIdx);
                                                writer.Write(f.IsSet ? (ulong)1 : (ulong)0);
                                            }
                                        }
                                    }
                                }
                            }
                            break;

                        case Src_HiddenItemFlags:
                            {
                                List<byte[]> tBlocks = new List<byte[]>(HiddenItemsBlockKeys.Length);
                                foreach (var _ in HiddenItemsBlockKeys)
                                    tBlocks.Add(savEventBlocks.GetBlockSafe(_).Data);

                                foreach (var f in fGroup.Flags)
                                {
                                    int k = (int)(f.FlagIdx >> 12);

                                    var data = tBlocks[k];
                                    data[(int)(f.FlagIdx & 1023)] = f.IsSet ? (byte)0 : (byte)0x80;
                                }
                            }
                            break;

                        case Src_TrainerFlags:
                            {
                                var bdata1 = savEventBlocks.GetBlockSafe(0xF018C4AC).Data;
                                var bdata2 = savEventBlocks.GetBlockSafe(0x28E475DE).Data;

                                using (var ms1 = new System.IO.MemoryStream(bdata1))
                                using (var ms2 = new System.IO.MemoryStream(bdata2))
                                {
                                    using (var writer1 = new System.IO.BinaryWriter(ms1))
                                    using (var writer2 = new System.IO.BinaryWriter(ms2))
                                    {
                                        foreach (var f in fGroup.Flags)
                                        {
                                            if (f.IsSet)
                                            {
                                                if (ms1.Position < ms1.Length)
                                                {
                                                    writer1.Write(f.FlagIdx);
                                                    writer1.Write(f.IsSet ? (ulong)1 : (ulong)0);
                                                }
                                                else if (ms2.Position < ms2.Length)
                                                {
                                                    writer2.Write(f.FlagIdx);
                                                    writer2.Write(f.IsSet ? (ulong)1 : (ulong)0);
                                                }
                                            }
                                        }

                                        // fill blanks
                                        while (ms1.Position < ms1.Length)
                                        {
                                            writer1.Write(0xCBF29CE484222645);
                                            writer1.Write((ulong)0);
                                        }
                                        while (ms2.Position < ms2.Length)
                                        {
                                            writer2.Write(0xCBF29CE484222645);
                                            writer2.Write((ulong)0);
                                        }

                                    }
                                }
                            }
                            break;
                    }

                    break;
                }
            }
        }

        public override void SyncEditedEventWork()
        {
            var savEventBlocks = ((ISCBlockArray)m_savFile!).Accessor;

            foreach (var w in m_eventWorkList)
            {
                savEventBlocks.GetBlockSafe((uint)w.WorkIdx).SetValue((int)w.Value);
            }
        }
    }
}
