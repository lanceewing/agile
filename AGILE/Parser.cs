using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace AGILE
{
    /// <summary>
    /// The Parser class is responsible for parsing the user input line to match known words and 
    /// also to implement the 'said' and 'parse' commands.
    /// </summary>
    class Parser
    {
        /// <summary>
        /// The List of word numbers for the recognised words from the current user input line.
        /// </summary>
        private List<int> RecognisedWordNumbers { get; set; }

        /// <summary>
        /// These are the characters that separate words in the user input string (although
        /// usually it would be space).
        /// </summary>
        private char[] SEPARATORS = " ,.?!();:[]{}".ToCharArray();

        /// <summary>
        /// A regex matching the characters to be deleted from the user input string.
        /// </summary>
        private string IGNORE_CHARS = "['`-\"]";

        /// <summary>
        /// Special word number that matches any word.
        /// </summary>
        private int ANYWORD = 1;

        /// <summary>
        /// Special word number that matches the rest of the line.
        /// </summary>
        private int REST_OF_LINE = 9999;

        /// <summary>
        /// The GameState class holds all of the data and state for the Game currently 
        /// being run by the interpreter.
        /// </summary>
        private GameState state;

        /// <summary>
        /// Constructor for Parser.
        /// </summary>
        /// <param name="state">The GameState class holds all of the data and state for the Game currently being run.</param>
        public Parser(GameState state)
        {
            this.state = state;
            this.RecognisedWordNumbers = new List<int>();
        }

        /// <summary>
        /// Parses the given user input line value. This is the method invoked by the main keyboard 
        /// processing logic. After execution of this method, the RecognisedWords List will contain
        /// the words that were recognised from the input line, and the RecognisedWordNumbers List
        /// will contain the word numbers for the recognised words. If the RecognisedWords List 
        /// contains one more item than the RecognisedWordNumbers List then the additional word
        /// will actually be an unrecognised word and the UNKNOWN_WORD var will contain the index
        /// of that word within the List + 1. The INPUT flag will be set if the RecognisedWords
        /// List contains at least one word.
        /// </summary>
        /// <param name="inputLine"></param>
        public void Parse(string inputLine)
        {
            bool lastWordIgnored = false;

            // Clear the words matched from last time.
            state.RecognisedWords.Clear();
            this.RecognisedWordNumbers.Clear();

            // LSL1 has word that contain spaces. e.g "al sent me". Let's check that first
            // before tokenizing the string
            // sanitize the string: lower case, trimm, remove excess white space between words
            string sanitizedLine = String.Join(" ", inputLine.ToLower().Trim().Split(' ').Select(p => p.Trim()).Where(p => !string.IsNullOrWhiteSpace(p)));
            if (state.Words.WordToNumber.ContainsKey(sanitizedLine))
            {
                // The word is recognised, so let's get the word number for it.
                int matchedWordNum = state.Words.WordToNumber[sanitizedLine];

                // If the word number is 0, it is ignored.
                if (matchedWordNum > 0)
                {
                    // Otherwise store matched word details.
                    state.RecognisedWords.Add(sanitizedLine);
                    this.RecognisedWordNumbers.Add(matchedWordNum);

                    state.Vars[Defines.UNKNOWN_WORD] = 0;
                    state.Flags[Defines.INPUT] = true;

                    // return from function. No need to tokenize input
                    return;
                }
            }


            // Remove ignored characters, then split user entered line by separators, retaining any non-empty results.
            IEnumerable<string> inputWords = inputLine.ToLower().Replace(IGNORE_CHARS, "").Split(SEPARATORS).Where(s => s.Length > 0);

            // Iterate through the user entered words attempting to match them to the known words.
            foreach (string inputWord in inputWords)
            {
                string wordToMatch = inputWord;

                // Start by checking if there is a match for the combination of the previous word
                // and this word joined together (e.g. "look inside"). Sometimes AGI defines such words.
                // But we only do this if there wasn't an ignored word in between.
                if ((state.RecognisedWords.Count > 0) && !lastWordIgnored) {
                    string joinedWord = state.RecognisedWords.Last() + " " + inputWord;
                    if (state.Words.WordToNumber.ContainsKey(joinedWord))
                    {
                        // The joined word is recognised, so we'll use that as the input word.
                        wordToMatch = joinedWord;

                        // And we'll also remove the previous match (since we now have a longer match).
                        state.RecognisedWords.RemoveAt(state.RecognisedWords.Count - 1);
                        this.RecognisedWordNumbers.RemoveAt(this.RecognisedWordNumbers.Count - 1);
                    }
                }

                lastWordIgnored = false;

                if (state.Words.WordToNumber.ContainsKey(wordToMatch))
                {
                    // The word is recognised, so let's get the word number for it.
                    int matchedWordNum = state.Words.WordToNumber[wordToMatch];

                    // If the word number is 0, it is ignored.
                    if (matchedWordNum > 0)
                    {
                        // Otherwise store matched word details.
                        state.RecognisedWords.Add(wordToMatch);
                        this.RecognisedWordNumbers.Add(matchedWordNum);
                    }
                    else
                    {
                        lastWordIgnored = true;
                    }
                }
                else if (!wordToMatch.Equals("a") && !wordToMatch.Equals("i"))
                {
                    // Unrecognised word. Stores the word, use ANYWORD (word number 1, place holder for any word)
                    state.RecognisedWords.Add(wordToMatch);
                    this.RecognisedWordNumbers.Add(ANYWORD);
                    state.Vars[Defines.UNKNOWN_WORD] = (byte)(state.RecognisedWords.Count);
                    break;
                }
            }

            if (state.RecognisedWords.Count > 0)
            {
                state.Flags[Defines.INPUT] = true;
            }
        }

        /// <summary>
        /// Implements the 'parse' AGI command. What it does is to parse a string as if it
        /// was the normal user input line. It does this simply by calling the Parse method 
        /// above with the value from the identified AGI string. It resets both the INPUT
        /// and HADMATCH flags prior to calling it so that the normal user input parsing
        /// state is cleared. The words will be available to all said() tests for the 
        /// remainder of the current logic scan.
        /// </summary>
        /// <param name="strNum">The number of the AGI string to parse the value of.</param>
        public void ParseString(int strNum)
        {
            // Clear the state from the most recent parse.
            state.Flags[Defines.INPUT] = false;
            state.Flags[Defines.HADMATCH] = false;

            // If the given string number is less that the total number of strings.
            if (strNum < Defines.NUMSTRINGS)
            {
                // Parse the value of the string as if it was user input.
                Parse(state.Strings[strNum]);
            }
        }

        /// <summary>
        /// Returns true if the number of non-ignored words in the input line is the same
        /// as that in the word list and the non-ignored words in the input match, in order, 
        /// the words in the word list. The special word 'anyword' (or whatever is defined 
        /// word list as word 1 in 'WORDS.TOK') matches any non-ignored word in the input.
        /// </summary>
        /// <param name="words">The List of words to test if the user has said.</param>
        /// <returns>true if the user has said the given words; otherwise false.</returns>
        public bool Said(List<int> wordNumbers)
        {
            // If there are no recognised words then we obviously didn't say what we're testing against.
            if (this.RecognisedWordNumbers.Count == 0) return false;

            // We should only perform the check if we have input, and there hasn't been a match already.
            if (!state.Flags[Defines.INPUT] || state.Flags[Defines.HADMATCH]) return false;

            // Compare each word number in order.
            for (int i=0; i < wordNumbers.Count; i++)
            {
                int testWordNumber = wordNumbers[i];

                // If test word number matches the rest of the line, then it's a match.
                if (testWordNumber == REST_OF_LINE)
                {
                    state.Flags[Defines.HADMATCH] = true;
                    return true;
                }

                // Exit if we have reached the end of the user entered words. No match.
                if (i >= RecognisedWordNumbers.Count) return false;

                int inputWordNumber = this.RecognisedWordNumbers[i];

                // If word numbers don't match, and test word number doesn't represent anyword, then no match.
                if ((testWordNumber != inputWordNumber) && (testWordNumber != ANYWORD)) return false;
            }

            // If more words were entered than in the said, and there obviously wasn't a REST_OF_LINE, then no match.
            if (state.RecognisedWords.Count > wordNumbers.Count) return false;

            // Otherwise if we get this far without having exited already, it is a match.
            state.Flags[Defines.HADMATCH] = true;
            return true;
        }
    }
}
