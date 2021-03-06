﻿/*
 * OpaqueMail (https://opaquemail.org/).
 * 
 * Licensed according to the MIT License (http://mit-license.org/).
 * 
 * Copyright © Bert Johnson (https://bertjohnson.com/) of Allcloud Inc. (https://allcloud.com/).
 * 
 * Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the “Software”), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:
 * 
 * The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.
 * 
 * THE SOFTWARE IS PROVIDED “AS IS”, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
 * 
 */

using System;
using System.Collections.Generic;

namespace OpaqueMail
{
    /// <summary>
    /// Represents an IMAP mailbox.
    /// </summary>
    public class Mailbox
    {
        /// <summary>List of FETCH commands returned by QRESYNC.</summary>
        public List<string> FetchList { get; set; }
        /// <summary>Standard IMAP flags associated with this mailbox.</summary>
        public HashSet<string> Flags { get; set; }
        /// <summary>Mailbox hierarchy delimiting string.</summary>
        public string HierarchyDelimiter { get; set; }
        /// <summary>Name of the mailbox.</summary>
        public string Name { get; set; }
        /// <summary>True if ModSeq is explicitly unavailable.</summary>
        public bool NoModSeq { get; set; }
        /// <summary>Permanent IMAP flags associated with this mailbox.</summary>
        public HashSet<string> PermanentFlags { get; set; }
        /// <summary>List of message IDs that have disappeared since the last QRESYNC.</summary>
        public string VanishedList { get; set; }

        /// <summary>Number of messages in the mailbox.  Null if COUNT was not parsed.</summary>
        public int? Count { get; set; }
        /// <summary>Highest ModSeq in the mailbox.  Null if HIGHESTMODSEQ was not parsed.</summary>
        public int? HighestModSeq { get; set; }
        /// <summary>Number of recent messages in the mailbox.  Null if RECENT was not parsed.</summary>
        public int? Recent { get; set; }
        /// <summary>Expected next UID for the mailbox.  Null if UIDNEXT was not parsed.</summary>
        public int? UidNext { get; set; }
        /// <summary>UID validity for the mailbox.  Null if UIDVALIDITY was not parsed.</summary>
        public int? UidValidity { get; set; }

        #region Constructors
        /// <summary>
        /// Default constructor.
        /// </summary>
        public Mailbox()
        {
            FetchList = new List<string>();
            Flags = new HashSet<string>();
            NoModSeq = false;
            PermanentFlags = new HashSet<string>();

            Count = null;
            HighestModSeq = null;
            Recent = null;
            UidNext = null;
            UidValidity = null;
        }

        /// <summary>
        /// Parse IMAP output from an EXAMINE or SELECT command.
        /// </summary>
        /// <param name="name">Name of the mailbox.</param>
        /// <param name="imapResponse">Raw IMAP output of an EXAMINE or SELECT command.</param>
        public Mailbox(string name, string imapResponse)
            : this()
        {
            // Escape modifed UTF-7 encoding for ampersands or Unicode characters.
            Name = Functions.FromModifiedUTF7(name);

            string[] responseLines = imapResponse.Replace("\r", "").Split('\n');
            foreach (string responseLine in responseLines)
            {
                if (responseLine.StartsWith("* FLAGS ("))
                {
                    string[] flags = responseLine.Substring(9, responseLine.Length - 10).Split(' ');
                    foreach (string flag in flags)
                    {
                        if (!Flags.Contains(flag))
                            Flags.Add(flag);
                    }
                }
                else if (responseLine.StartsWith("* OK [NOMODSEQ]"))
                    NoModSeq = true;
                else if (responseLine.StartsWith("* OK [HIGHESTMODSEQ "))
                {
                    string highestModSeq = responseLine.Substring(20, responseLine.IndexOf("]") - 20);
                    int highestModSeqValue;
                    int.TryParse(highestModSeq, out highestModSeqValue);
                    HighestModSeq = highestModSeqValue;
                }
                else if (responseLine.StartsWith("* OK [PERMANENTFLAGS ("))
                {
                    string[] permanentFlags = responseLine.Substring(22, responseLine.IndexOf("]") - 22).Split(' ');
                    foreach (string permanentFlag in permanentFlags)
                    {
                        if (!PermanentFlags.Contains(permanentFlag))
                            PermanentFlags.Add(permanentFlag);
                    }
                }
                else if (responseLine.StartsWith("* OK [UIDNEXT "))
                {
                    string uidNext = responseLine.Substring(14, responseLine.IndexOf("]") - 14);
                    int uidNextValue;
                    int.TryParse(uidNext, out uidNextValue);
                    UidNext = uidNextValue;
                }
                else if (responseLine.StartsWith("* OK [UIDVALIDITY "))
                {
                    string uidValidity = responseLine.Substring(18, responseLine.IndexOf("]") - 18);
                    int UidValidityValue;
                    int.TryParse(uidValidity, out UidValidityValue);
                    UidValidity = UidValidityValue;
                }
                else if (responseLine.StartsWith("* VANISHED "))
                    VanishedList = responseLine.Substring(11);
                else if (responseLine.IndexOf(" FETCH ", StringComparison.Ordinal) > -1)
                    FetchList.Add(responseLine);
                else if (responseLine.EndsWith(" EXISTS"))
                {
                    string existsCount = responseLine.Substring(2, responseLine.Length - 9);
                    int existsCountValue;
                    int.TryParse(existsCount, out existsCountValue);
                    Count = existsCountValue;
                }
                else if (responseLine.EndsWith(" RECENT"))
                {
                    string recentCount = responseLine.Substring(2, responseLine.Length - 9);
                    int recentCountValue;
                    int.TryParse(recentCount, out recentCountValue);
                    Recent = recentCountValue;
                }
            }
        }

        /// <summary>
        /// Parse IMAP output from a LIST, LSUB, or XLIST command.
        /// </summary>
        /// <param name="lineFromListCommand">Raw output line from a LIST, LSUB, or XLIST command.</param>
        /// <returns></returns>
        public static Mailbox CreateFromList(string lineFromListCommand)
        {
            // Ensure the list of flags is contained on this line.
            int startsFlagList = lineFromListCommand.IndexOf("(");
            int endFlagList = lineFromListCommand.IndexOf(")", startsFlagList + 1);
            if (startsFlagList > -1 && endFlagList > -1)
            {
                Mailbox mailbox = new Mailbox();

                string[] flags = lineFromListCommand.Substring(startsFlagList + 1, endFlagList - startsFlagList - 1).Split(' ');
                foreach (string flag in flags)
                {
                    if (!mailbox.Flags.Contains(flag))
                        mailbox.Flags.Add(flag);
                }

                // Ensure the hierarchy delimiter and name are returned.
                string[] remainingParts = lineFromListCommand.Substring(endFlagList + 2).Split(new char[] { ' ' }, 2);
                if (remainingParts.Length == 2)
                {
                    mailbox.HierarchyDelimiter = remainingParts[0].Replace("\"", "");

                    // Escape modifed UTF-7 encoding for ampersands or Unicode characters.
                    mailbox.Name = Functions.FromModifiedUTF7(remainingParts[1].Replace("\"", ""));

                    return mailbox;
                }
            }

            // No valid mailbox listing found, so return null.
            return null;
        }
        #endregion Constructors
    }
}
