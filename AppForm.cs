using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Linq;
using System.Text;
using System.IO;
using System.Windows.Forms;
using CsvHelper;
using System.Globalization;
using Npgsql;
using TextPrerocessing;
using System.Threading.Tasks;

namespace TextPreprocessing
{
    public partial class AppForm : Form
    {
        public AppForm()
        {
            InitializeComponent();
        }
        private NpgsqlConnection connection;
        private static string textFilePath;
        private static string text;
        private static string folderPath;
        private static string folderPathDialog;
        private static string dateTime;

        private const string CONNECTION_STRING = "Host=localhost:1025;" +
                                                 "Username=postgres;" +
                                                 "Password=13467;" +
                                                 "Database=postgres";

        private static readonly string alphabetru   = "абвгдеёжзийклмнопрстуфхцчшщъыьэюя";
        private static readonly string alphabeteng  = "abcdefghijklmnopqrstuvwxyz";

        private static readonly HashSet<char> Glasru    = new HashSet<char>("аеёиоуыэяю"),
                                              Zvonk     = new HashSet<char>("бвгджзйлмнр"),
                                              Soglasru  = new HashSet<char>("бвгджзклмнпрстфхцчшщъь"),
                                              Gluh      = new HashSet<char>("кпстфхцчшщ");

        private static readonly HashSet<char> Glaseng   = new HashSet<char>("aeiouy"),
                                              Soglaseng = new HashSet<char>("bcdfghjklmnpqrstvwxz");

        private static readonly char[] separator = new char[] { ',', '.', '?', '-', '!', '—', ';', ' ', ':',
            '\r', '\n', '\t', '\a', '\b', '\f', '\v', '\'', '\"', '«', '»',
            '`', '~', '>', '<', '(', ')', '=', '+', '*', '&', '^', '%', '$', '#', '@', '№', '/', '\\', '|',
            '1', '2', '3', '4', '5', '6', '7', '8', '9', '0'};

        private static readonly List<string> stopWordsru = new List<string> {
            "а", "б", "в", "г", "д", "е", "ж", "з", "и", "й", "к", "л", "м", "н", "о", "п", "р", "с", "т", "у", "ф", "х", "ц", "ч", "ш", "щ", "ъ", "ы", "ь", "э", "ю",
            "со", "над", "на" ,"по" , "под", "где", "там", "сям", "ни", "не", "да", "за", "ту", "та", "те", "как", "так", "сяк",
            "ах", "ох", "ух", "эх", "эй", "ай", "ой", "чем", "тем", "но", "мы", "мой", "вы", "ваш"
        };

        private static readonly List<string> stopWordseng = new List<string> {
            "b", "c", "d", "e", "f", "g", "h", "j", "k", "l", "m", "n", "o", "p", "q", "r", "s", "t", "u", "v", "w", "x", "y", "z"
        };
        private const string SENTENCES_FANO = "SentencesFano";
        public void NpgsqlBoardGameRepository()
        {
            connection = new NpgsqlConnection(CONNECTION_STRING);
            connection.Open();
        }
        public async void Add(SentencesFano senF)
        {
            string commandText = $"INSERT INTO {SENTENCES_FANO} (name, data_time, count, count_unic, entropy_2, count_elementary_2, info_count_2, entropy_3, count_elementary_3, info_count_3, entropy_5, count_elementary_5, info_count_5) VALUES (@name, @data_time, @count, @count_unic, @entropy_2, @count_elementary_2, @info_count_2, @entropy_3, @count_elementary_3, @info_count_3, @entropy_5, @count_elementary_5, @info_count_5)";
            await using (NpgsqlCommand cmd = new NpgsqlCommand(commandText, connection))
            {
                cmd.Parameters.AddWithValue("id", game.Id);
                cmd.Parameters.AddWithValue("name", game.Name);
                cmd.Parameters.AddWithValue("minPl", game.MinPlayers);
                cmd.Parameters.AddWithValue("maxPl", game.MaxPlayers);
                cmd.Parameters.AddWithValue("avgDur", game.AverageDuration);

                await cmd.ExecuteNonQueryAsync();
            }
        }
        private void CalculateBtn_Click(object sender, EventArgs e)
        {

            if (string.IsNullOrEmpty(textFilePath))
            {
                MessageBox.Show("Необходимо указать файл с текстом", "Внимание", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                return;
            }
            if (string.IsNullOrEmpty(folderPathDialog) || !Directory.Exists(folderPathDialog))
            {
                MessageBox.Show("Путь к папке не найден. Необходимо указать папку для записи результатов", "Внимание", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                return;
            }
            if (!radioButton1.Checked && !radioButton2.Checked)
            {
                MessageBox.Show("Необходимо выбрать язык текста", "Внимание", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                return;
            }
            if (textBox73.Text.Equals(""))
            {
                MessageBox.Show("Необходимо ввести имя пользователя", "Внимание", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                return;
            }
            if (Fanofor2Check.Checked == false && checkBoxforLetters.Checked == false && Huffmanfor2Check.Checked == false && Fanofor3Check.Checked == false && Huffmanfor3Check.Checked == false && Fanofor5Check.Checked == false && Huffmanfor5Check.Checked == false && checkBoxforSen.Checked == false && checkBoxforWords.Checked == false && checkBoxforSyllables.Checked == false)
            {
                MessageBox.Show("Необходимо выбрать способ и метод кодирования для проведения рассчетов", "Внимание", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                return;
            }
            
            dateTime = DateTime.Now.ToString().Replace('/', '.').Replace(':', '.');

            using (StreamReader streamReader = new StreamReader(textFilePath, Encoding.UTF8))
            {
                text = streamReader.ReadToEnd().Trim().Replace("\r\n", " ");
            }

            resultTextBox.Text = "Символов в исходном тексте: " + text.Length + "\r\n\r\n";

            if (radioButton1.Checked)
            {
                // Деление текста на предложения
                string[] sentencesSplit = Regex.Split(text, @"(?<=[\.!\?{:\r}])\s+");
                int sc                  = sentencesSplit.Count();
                List<string> sentences  = new List<string>();

                for (int i = 0; i < sc; i++)
                {
                    string firstSymbol  = sentencesSplit[i][0].ToString();
                    if (firstSymbol     == firstSymbol.ToLower() && i > 0 && alphabetru.Contains(firstSymbol.ToLower()))
                    {
                        string lastCentences = sentences.Last();
                        sentences.RemoveAt(sentences.Count - 1);
                        sentences.Add(lastCentences + " " + sentencesSplit[i]);
                        continue;
                    }
                    sentences.Add(sentencesSplit[i]);
                }
                resultTextBox.Text += "Шаг 1. Токенизация. Удаление лишних символов\r\n";

                var words = text.ToLower().Split(separator, StringSplitOptions.RemoveEmptyEntries).ToList();

                resultTextBox.Text += "Символов: " + GetLettersCountInText(words) + "\r\n";
                resultTextBox.Text += "Слов: " + words.Count + "\r\n\r\n";

                resultTextBox.Text += "Шаг 2. Стоп-лист. Удаление слов из стоп листа и слов, содержащих символы не русского алфавита\r\n";

                words.RemoveAll(word    => stopWordsru.Any(stopWord => word == stopWord) || CheckLetterRu(word));
                if (words.Count         == 0)
                {
                    resultTextBox.Text += "Программа остановлена, количество слов равно 0\r\n";
                    return;
                }

                resultTextBox.Text += "Слов: " + words.Count + "\r\n\r\n";
                resultTextBox.Text += "Шаг 3. Деление на слоги\r\n";

                var syllables       = new List<string>();
                words.ForEach(word  => GetSyllablesRu(word).ToList().ForEach(el => syllables.Add(el)));

                resultTextBox.Text     += "Слогов: " + syllables.Count + "\r\n\r\n";
                int lettersCountInText  = GetLettersCountInText(words);

                //встречаемость букв+частота
                var letterCountDict     = GetLetterCount(words).OrderByDescending(pair => pair.Value).ToDictionary(pair => pair.Key, pair => pair.Value);
                var probabilityLetter   = GetProbability(letterCountDict, lettersCountInText).OrderByDescending(pair => pair.Value).ToDictionary(pair => pair.Key, pair => pair.Value);

                //встречаемость слогов+частота
                var syllablesCountDict      = syllables.GroupBy(el => el).ToDictionary(el => el.Key, el => el.Count()).OrderByDescending(pair => pair.Value).ToDictionary(pair => pair.Key, pair => pair.Value);
                var probabilitySyllables    = GetProbability(syllablesCountDict, syllables.Count).OrderByDescending(pair => pair.Value).ToDictionary(pair => pair.Key, pair => pair.Value);

                //Встречаемость предложений+частота
                var sentencesCountDict      = sentences.GroupBy(el => el).ToDictionary(el => el.Key, el => el.Count()).OrderByDescending(pair => pair.Value).ToDictionary(pair => pair.Key, pair => pair.Value);
                var probabilitySentences    = GetProbability(sentencesCountDict, sentences.Count).OrderByDescending(pair => pair.Value).ToDictionary(pair => pair.Key, pair => pair.Value);

                //Встречаемость слов + частота
                var wordsCountDict      = words.GroupBy(el => el).ToDictionary(el => el.Key, el => el.Count()).OrderByDescending(pair => pair.Value).ToDictionary(pair => pair.Key, pair => pair.Value);
                var probabilityWords    = GetProbability(wordsCountDict, words.Count).OrderByDescending(pair => pair.Value).ToDictionary(pair => pair.Key, pair => pair.Value);

                double unicword = wordsCountDict.Count;//количество уникальных слов
                double unicsyllables = syllablesCountDict.Count;//количество уникальных слогов
                double unicletter = letterCountDict.Count;//количество букв алфавита
                double unicsen = sentencesCountDict.Count;//количество уникальных предложений

                //Энтропия предложений
                var entropySentences2           = GetEntropy(probabilitySentences);//расчет энтропии предложений при кодировании 2сс
                var entropySenteces3            = GetEntropy3(probabilitySentences);//расчет энтропии предложений при кодировании 3 сс
                var entropySenteces5            = GetEntropy5(probabilitySentences);//расчет энтропии предложений при кодировании 5 сс
                var entropySentencesSen         = GetEntropyLec(probabilitySentences, unicsen);//расчет энтропии предложений при кодировании предложениями
                var entropySentencesW           = GetEntropyLec(probabilitySentences, unicword);//расчет энтропии предложений при кодировании словами
                var entropySentecesS            = GetEntropyLec(probabilitySentences, unicsyllables);//расчет энтропии предложений при кодировании слогами
                var entropySentecesL            = GetEntropyLec(probabilitySentences, unicletter);//расчет энтропии предложений при кодировании буквами
                
                //Энтропия слов
                var entropyWords  = GetEntropy(probabilityWords);//расчет энтропии при кодировании 2сс
                var entropyWords3 = GetEntropy3(probabilityWords);//расчет энтропии при кодировании 3сс
                var entropyWords5 = GetEntropy5(probabilityWords);//расчет энтропии при кодировании 5сс
                var entropyWordsS = GetEntropyLec(probabilityWords, unicsyllables);//расчет энтропии слов при кодировании слогамми
                var entropyWordsL = GetEntropyLec(probabilityWords, unicletter);//расчет энтропии слов при кодировании буквами
                var entropyWordsW = GetEntropyLec(probabilityWords, unicword);//расчет энтропии слов при кодировании словами
                
                //Энтропия слогов
                var entropyS            = GetEntropyLec(probabilitySyllables, unicsyllables);//расчет энтропии слогов при кодировании слогами
                var entropySyllablesL   = GetEntropyLec(probabilitySyllables, unicletter);//расчет энтропии слогов при кодировании буквами
                var entropySyllables    = GetEntropy(probabilitySyllables);//расчет энтропии при кодировании 2сс
                var entropySyllables3   = GetEntropy3(probabilitySyllables);//расчет энтропии при кодировании 3сс
                var entropySyllables5   = GetEntropy5(probabilitySyllables);//расчет энтропии при кодировании 5сс
                
                //Энтропия букв
                var entropyLetter  = GetEntropy(probabilityLetter);//расчет энтропии букв при кодировании 2сс
                var entropyLetter3 = GetEntropy3(probabilityLetter);//расчет энтропии при кодировании 3сс
                var entropyLetter5 = GetEntropy5(probabilityLetter);//расчет энтропии при кодировании 5сс
                var entropyLetterL = GetEntropyLec(probabilityLetter, unicletter);//расчет энтропии при кодировании буквами
                
                resultTextBox.Text += "Шаг 4. Вычисления\r\n";

                //Фано
                string[] sentencesFano      = new string[(int)unicsen];
                string[] sentencesFano3     = new string[(int)unicsen];
                string[] sentencesFano5     = new string[(int)unicsen];
                string[] wordsFano          = new string[(int)unicword];
                string[] wordsFano3         = new string[(int)unicword];
                string[] wordsFano5         = new string[(int)unicword];
                string[] syllablesFano      = new string[(int)unicsyllables];
                string[] syllablesFano3     = new string[(int)unicsyllables];
                string[] syllablesFano5     = new string[(int)unicsyllables];
                string[] lettersFano        = new string[(int)unicletter];
                string[] lettersFano3       = new string[(int)unicletter];
                string[] lettersFano5       = new string[(int)unicletter];

                Fano(0, (int)unicsen - 1, sentencesFano, probabilitySentences);
                Fanofor3(0, (int)unicsen - 1, sentencesFano3, probabilitySentences);
                Fanofor5(0, (int)unicsen - 1, sentencesFano5, probabilitySentences);
                Fano(0, (int)unicword - 1, wordsFano, probabilityWords);
                Fanofor3(0, (int)unicword - 1, wordsFano3, probabilityWords);
                Fanofor5(0, (int)unicword - 1, wordsFano5, probabilityWords);
                Fano(0, (int)unicsyllables - 1, syllablesFano, probabilitySyllables);
                Fanofor3(0, (int)unicsyllables - 1, syllablesFano3, probabilitySyllables);
                Fanofor5(0, (int)unicsyllables - 1, syllablesFano5, probabilitySyllables);
                Fano(0, (int)unicletter - 1, lettersFano, probabilityLetter);
                Fanofor3(0, (int)unicletter - 1, lettersFano3, probabilityLetter);
                Fanofor5(0, (int)unicletter - 1, lettersFano5, probabilityLetter);

                //Хаффмен
                var letterCountDictS = GetLetterCountInStringFormat(letterCountDict);

                string[] sentencesHuffman   = new string[(int)unicsen];
                string[] sentencesHuffman3  = new string[(int)unicsen];
                string[] sentencesHuffman5  = new string[(int)unicsen];
                string[] wordsHuffman       = new string[(int)unicword];
                string[] wordsHuffman3      = new string[(int)unicword];
                string[] wordsHuffman5      = new string[(int)unicword];
                string[] syllablesHuffman   = new string[(int)unicsyllables];
                string[] syllablesHuffman3  = new string[(int)unicsyllables];
                string[] syllablesHuffman5  = new string[(int)unicsyllables];
                string[] lettersHuffman     = new string[(int)unicletter];
                string[] lettersHuffman3    = new string[(int)unicletter];
                string[] lettersHuffman5    = new string[(int)unicletter];

                sentencesHuffman    = CompressBytes(sentencesCountDict, sentencesHuffman);
                sentencesHuffman3   = CompressBytes3(sentencesCountDict, sentencesHuffman3);
                sentencesHuffman5   = CompressBytes5(sentencesCountDict, sentencesHuffman5);
                wordsHuffman        = CompressBytes(wordsCountDict, wordsHuffman);
                wordsHuffman3       = CompressBytes3(wordsCountDict, wordsHuffman3);
                wordsHuffman5       = CompressBytes5(wordsCountDict, wordsHuffman5);
                syllablesHuffman    = CompressBytes(syllablesCountDict, syllablesHuffman);
                syllablesHuffman3   = CompressBytes3(syllablesCountDict, syllablesHuffman3);
                syllablesHuffman5   = CompressBytes5(syllablesCountDict, syllablesHuffman5);
                lettersHuffman      = CompressBytes(letterCountDictS, lettersHuffman);
                lettersHuffman3     = CompressBytes3(letterCountDictS, lettersHuffman3);
                lettersHuffman5     = CompressBytes5(letterCountDictS, lettersHuffman5);

                //расчет среднего количества уникальных символов
                double sentencesFanoElementaryCount         = CountElementaryCharOnOneEncodeChar(sentencesFano, probabilitySentences);
                double sentencesFanoElementaryCount3        = CountElementaryCharOnOneEncodeChar(sentencesFano3, probabilitySentences);
                double sentencesFanoElementaryCount5        = CountElementaryCharOnOneEncodeChar(sentencesFano5, probabilitySentences);
                double sentencesHuffmanElementaryCount      = CountElementaryCharOnOneEncodeChar(sentencesHuffman, probabilitySentences);
                double sentencesHuffmanElementaryCount3     = CountElementaryCharOnOneEncodeChar(sentencesHuffman3, probabilitySentences);
                double sentencesHuffmanElementaryCount5     = CountElementaryCharOnOneEncodeChar(sentencesHuffman5, probabilitySentences);
                double sentencesFanoElementaryCountS        = CountElementarySentences(probabilitySentences);
                double wordsinsen                           = CountElementaryWord(probabilitySentences);
                double wordsFanoElementaryCountW            = CountElementarySentences(probabilityWords);
                double wordsFanoElementaryCount             = CountElementaryCharOnOneEncodeChar(wordsFano, probabilityWords);
                double wordsFanoElementaryCount3            = CountElementaryCharOnOneEncodeChar(wordsFano3, probabilityWords);
                double wordsFanoElementaryCount5            = CountElementaryCharOnOneEncodeChar(wordsFano5, probabilityWords);
                double wordsHuffmanElementaryCount          = CountElementaryCharOnOneEncodeChar(wordsHuffman, probabilityWords);
                double wordsHuffmanElementaryCount3         = CountElementaryCharOnOneEncodeChar(wordsHuffman3, probabilityWords);
                double wordsHuffmanElementaryCount5         = CountElementaryCharOnOneEncodeChar(wordsHuffman5, probabilityWords);
                double syllablesinsen                       = CountElementarySylla(probabilitySentences);
                double syllablesinwords                     = CountElementarySylla(probabilityWords);
                double syllablesFanoElementaryCount         = CountElementaryCharOnOneEncodeChar(syllablesFano, probabilitySyllables);
                double syllablesFanoElementaryCount3        = CountElementaryCharOnOneEncodeChar(syllablesFano3, probabilitySyllables);
                double syllablesFanoElementaryCount5        = CountElementaryCharOnOneEncodeChar(syllablesFano5, probabilitySyllables);
                double syllablesHuffmanElementaryCount      = CountElementaryCharOnOneEncodeChar(syllablesHuffman, probabilitySyllables);
                double syllablesHuffmanElementaryCount3     = CountElementaryCharOnOneEncodeChar(syllablesHuffman3, probabilitySyllables);
                double syllablesHuffmanElementaryCount5     = CountElementaryCharOnOneEncodeChar(syllablesHuffman5, probabilitySyllables);
                double syllablesFanoElementaryCountS        = CountElementarySentences(probabilitySyllables);
                double lettersFanoElementaryCount           = CountElementaryCharOnOneEncodeChar(lettersFano, probabilityLetter);
                double letteraFanoElementaryCount3          = CountElementaryCharOnOneEncodeChar(lettersFano3, probabilityLetter);
                double lettersFanoElementaryCount5          = CountElementaryCharOnOneEncodeChar(lettersFano5, probabilityLetter);
                double lettersHuffmanElementaryCount        = CountElementaryCharOnOneEncodeChar(lettersHuffman, probabilityLetter);
                double lettersHuffmanElementaryCount3       = CountElementaryCharOnOneEncodeChar(lettersHuffman3, probabilityLetter);
                double lettersHuffmanElementaryCount5       = CountElementaryCharOnOneEncodeChar(lettersHuffman5, probabilityLetter);
                double lettersFanoElementaryCountL          = CountElementaryLec(probabilityLetter);
                double letterinsen                          = CountElementaryLet(probabilitySentences);
                double letterinwords                        = CountElementaryLet(probabilityWords);
                double letterinsyllables                    = CountElementaryLet(probabilitySyllables);

                //Кол-в информации на 1 симол
                double sentencesInfoCount       = GetInfoCount(entropySentences2, sentencesFanoElementaryCount);
                double sentencesInfoCount3      = GetInfoCount(entropySenteces3, sentencesFanoElementaryCount3);
                double sentencesInfoCount5      = GetInfoCount(entropySenteces5, sentencesFanoElementaryCount5);
                double sentencesInfoHufCount    = GetInfoCount(entropySentences2, sentencesHuffmanElementaryCount);
                double sentencesInfoHufCount3   = GetInfoCount(entropySenteces3, sentencesHuffmanElementaryCount3);
                double sentencesInfoHufCount5   = GetInfoCount(entropySenteces5, sentencesHuffmanElementaryCount5);
                double sentencesInfoCountS      = GetInfoCount(entropySentencesSen, sentencesFanoElementaryCountS);
                double sentencesInfoCountSyl    = GetInfoCount(entropySentecesS, syllablesinsen);
                double sentencesInfoCountW      = GetInfoCount(entropySentencesW, wordsinsen);
                double sentencesInfoCountL      = GetInfoCount(entropySentecesL, letterinsen);
                double wordsInfoCountW          = GetInfoCount(entropyWordsW, wordsFanoElementaryCountW);
                double wordsInfoCount           = GetInfoCount(entropyWords, wordsFanoElementaryCount);
                double wordsInfoCount3          = GetInfoCount(entropyWords3, wordsFanoElementaryCount3);
                double wordsInfoCount5          = GetInfoCount(entropyWords5, wordsFanoElementaryCount5);
                double wordsInfoHufCount        = GetInfoCount(entropyWords, wordsHuffmanElementaryCount);
                double wordsInfoHufCount3       = GetInfoCount(entropyWords3, wordsHuffmanElementaryCount3);
                double wordsInfoHufCount5       = GetInfoCount(entropyWords5, wordsHuffmanElementaryCount5);
                double wordsInfoCountS          = GetInfoCount(entropyWordsS, syllablesinwords);
                double wordsInfoCountL          = GetInfoCount(entropyWordsL, letterinwords);
                double syllablesInfoCountS      = GetInfoCount(entropyS, syllablesFanoElementaryCountS);
                double syllablesInfoCount       = GetInfoCount(entropySyllables, syllablesFanoElementaryCount);
                double syllablesInfoCount3      = GetInfoCount(entropySyllables3, syllablesFanoElementaryCount3);
                double syllablesInfoCount5      = GetInfoCount(entropySyllables5, syllablesFanoElementaryCount5);
                double syllablesInfoHufCount    = GetInfoCount(entropySyllables, syllablesHuffmanElementaryCount);
                double syllablesInfoHufCount3   = GetInfoCount(entropySyllables3, syllablesHuffmanElementaryCount3);
                double syllablesInfoHufCount5   = GetInfoCount(entropySyllables5, syllablesHuffmanElementaryCount5);
                double syllablesInfoCountL      = GetInfoCount(entropySyllablesL, letterinsyllables);
                double lettersInfoCount         = GetInfoCount(entropyLetter, lettersFanoElementaryCount);
                double lettersInfoCount3        = GetInfoCount(entropyLetter3, letteraFanoElementaryCount3);
                double lettersInfoCount5        = GetInfoCount(entropyLetter5, lettersFanoElementaryCount5);
                double lettersInfoHufCount      = GetInfoCount(entropyLetter, lettersHuffmanElementaryCount);
                double lettersInfoHufCount3     = GetInfoCount(entropyLetter3, lettersHuffmanElementaryCount3);
                double lettersInfoHufCount5     = GetInfoCount(entropyLetter5, lettersHuffmanElementaryCount5);
                double lettersInfoCountL        = GetInfoCount(entropyLetterL, lettersFanoElementaryCountL);

                //вывод
                resultTextBox.Text += "Количество символов, всего в тексте: \r\n" +
                   "    Предложения    " + countSentencesTextBox.Text + "\r\n" +
                   "    Слова          " + countWordsTextBox.Text + "\r\n" +
                   "    Слоги          " + countSyllablesTextBox.Text + "\r\n" +
                   "    Буквы          " + countLetterTextBox.Text + "\r\n\r\n";

                resultTextBox.Text += "Количество уникальных символов: \r\n" +
                   "    Предложения    " + countSentencesUnicTextBox.Text + "\r\n" +
                   "    Слова          " + countWordsUnicTextBox.Text + "\r\n" +
                   "    Слоги          " + countSyllablesUnicTextBox.Text + "\r\n" +
                   "    Буквы          " + countLetterUnicTextBox.Text + "\r\n\r\n";

                if (Fanofor2Check.Checked == true || Fanofor3Check.Checked == true || Fanofor5Check.Checked == true)
                {
                    countSentencesTextBox.Text      = sentences.Count.ToString();
                    countSentencesUnicTextBox.Text  = unicsen.ToString();
                    countWordsTextBox.Text          = words.Count.ToString(); 
                    countWordsUnicTextBox.Text      = unicword.ToString();
                    countSyllablesTextBox.Text      = syllables.Count.ToString();
                    countSyllablesUnicTextBox.Text  = unicsyllables.ToString();
                    countLetterTextBox.Text         = lettersCountInText.ToString();
                    countLetterUnicTextBox.Text     = unicletter.ToString();
                }
                if (Fanofor2Check.Checked == true)
                {
                    // Вывод энтропии при Фано 2 сс
                    entropySentencesTextBox.Text        = String.Format("{0:f4}", entropySentences2);//вывод энтропия предложений при кодировании 2 сс
                    entropyWordsTextBox.Text            = String.Format("{0:f4}", entropyWords);//вывод энтропии при кодировании 2сс
                    entropySyllablesTextBox.Text        = String.Format("{0:f4}", entropySyllables);//вывод энтропии слогов при кодировании 2сс
                    entropyLetterTextBox.Text           = String.Format("{0:f4}", entropyLetter);//вывод энтропии при 2сс
                    
                    //Вывод количества элементарных символов при Фано 2 сс
                    SentencesElementaryTextBox.Text     = String.Format("{0:f4}", sentencesFanoElementaryCount);
                    WordsElementaryTextBox.Text         = String.Format("{0:f4}", wordsFanoElementaryCount);
                    SyllablesElementaryTextBox.Text     = String.Format("{0:f4}", syllablesFanoElementaryCount);
                    LettersElementaryTextBox.Text       = String.Format("{0:f4}", lettersFanoElementaryCount);
                    
                    //Вывод количества информации при Фано 2сс
                    sentencesInfoCountTextBox.Text      = String.Format("{0:f4}", sentencesInfoCount);
                    wordsInfoCountTextBox.Text          = String.Format("{0:f4}", wordsInfoCount);
                    syllablesInfoCountTextBox.Text      = String.Format("{0:f4}", syllablesInfoCount);
                    lettersInfoCountTextBox.Text        = String.Format("{0:f4}", lettersInfoCount);
                    
                    //Запись в лог
                    resultTextBox.Text += "Энтропия при кодировании в 2-ой системе счисления методом Шеннона-Фано: \r\n" +
                    "    Предложения    " + entropySentences2 + "\r\n" +
                    "    Слова          " + entropyWords + "\r\n" +
                    "    Слоги          " + entropySyllables + "\r\n" +
                    "    Буквы          " + entropyLetter + "\r\n\r\n";
                    resultTextBox.Text += "Среднее количество элементарных символов на 1 символ текста при кодировании в 2-ой системе счисления методом Шефанно-Фано: \r\n" +
                    "    Предложения    " + sentencesFanoElementaryCount + "\r\n" +
                    "    Слова          " + wordsFanoElementaryCount + "\r\n" +
                    "    Слоги          " + syllablesFanoElementaryCount + "\r\n" +
                    "    Буквы          " + lettersFanoElementaryCount + "\r\n\r\n";
                    resultTextBox.Text += "Количество информации на один элементарный символ при кодировании в 2-ой системе счисления методом Шефанно-Фано: \r\n" +
                    "    Предложения    " + sentencesInfoCount + "\r\n" +
                    "    Слова          " + wordsInfoCount + "\r\n" +
                    "    Слоги          " + syllablesInfoCount + "\r\n" +
                    "    Буквы          " + lettersInfoCount + "\r\n\r\n";
                }
                if (Fanofor3Check.Checked == true)
                {
                    // Вывод энтропии при Фано 3 сс
                    entropyFano3Sen.Text                = String.Format("{0:f4}", entropySenteces3);
                    entropyFano3W.Text                  = String.Format("{0:f4}", entropyWords3);
                    entropyFano3Syllables.Text          = String.Format("{0:f4}", entropySyllables3);
                    entropy3FanoLetters.Text            = String.Format("{0:f4}", entropyLetter3);
                    
                    //Вывод количества элементарных символов при Фано 3 сс
                    SenElementaryCountFano3.Text        = String.Format("{0:f4}", sentencesFanoElementaryCount3);
                    WordsElementaryCountFano3.Text      = String.Format("{0:f4}", wordsFanoElementaryCount3);
                    SyllablElementaryCountFano3.Text    = String.Format("{0:f4}", syllablesFanoElementaryCount3);
                    LettersElementaryCountFano3.Text    = String.Format("{0:f4}", letteraFanoElementaryCount3);
                    
                    //Вывод количества информации при Фано 3сс
                    InfoCountSenFano3.Text              = String.Format("{0:f4}", sentencesInfoCount3);
                    InfoCountWFano3.Text                = String.Format("{0:f4}", wordsInfoCount3);
                    InfoCountSyllablesFano3.Text        = String.Format("{0:f4}", syllablesInfoCount3);
                    InfoCountLettersFano3.Text          = String.Format("{0:f4}", lettersInfoCount3);
                    
                    //Запись в лог
                    resultTextBox.Text    += "Энтропия при кодировании в 3-ой системе счисления методом Шеннона-Фано: \r\n" +
                    "    Предложения    " + entropySenteces3 + "\r\n" +
                    "    Слова          " + entropyWords3 + "\r\n" +
                    "    Слоги          " + entropySyllables3 + "\r\n" +
                    "    Буквы          " + entropyLetter3 + "\r\n\r\n";
                    resultTextBox.Text    += "Среднее количество элементарных символов на 1 символ текста при кодировании в 3-ой системе счисления методом Шефанно-Фано: \r\n" +
                    "    Предложения    " + sentencesFanoElementaryCount3 + "\r\n" +
                    "    Слова          " + wordsFanoElementaryCount3 + "\r\n" +
                    "    Слоги          " + syllablesFanoElementaryCount3 + "\r\n" +
                    "    Буквы          " + letteraFanoElementaryCount3 + "\r\n\r\n";
                    resultTextBox.Text    += "Количество информации на один элементарный символ при кодировании в 3-ой системе счисления методом Шефанно-Фано: \r\n" +
                    "    Предложения    " + sentencesInfoCount3 + "\r\n" +
                    "    Слова          " + wordsInfoCount3 + "\r\n" +
                    "    Слоги          " + syllablesInfoCount3 + "\r\n" +
                    "    Буквы          " + lettersInfoCount3 + "\r\n\r\n";
                }
                if (Fanofor5Check.Checked == true)
                {
                    // Вывод энтропии при Фано 5 сс
                    entropyFano5Sen.Text                = String.Format("{0:f4}", entropySenteces5);
                    entropyFano5W.Text                  = String.Format("{0:f4}", entropyWords5);
                    entropyFano5Syllables.Text          = String.Format("{0:f4}", entropySyllables5);
                    entropy5FanoLetters.Text            = String.Format("{0:f4}", entropyLetter5);
                    
                    //Вывод количества элементарных символов при Фано 5 сс
                    SenElementaryCountFano5.Text        = String.Format("{0:f4}", sentencesFanoElementaryCount5);
                    WordsElementaryCountFano5.Text      = String.Format("{0:f4}", wordsFanoElementaryCount5);
                    SyllablElementaryCountFano5.Text    = String.Format("{0:f4}", syllablesFanoElementaryCount5);
                    LettersElementaryCountFano5.Text    = String.Format("{0:f4}", lettersFanoElementaryCount5);
                    
                    //Вывод количества информации при Фано 5сс
                    InfoCountSenFano5.Text              = String.Format("{0:f4}", sentencesInfoCount5);
                    InfoCountWFano5.Text                = String.Format("{0:f4}", wordsInfoCount5);
                    InfoCountSyllablesFano5.Text        = String.Format("{0:f4}", syllablesInfoCount5);
                    InfoCountLettersFano5.Text          = String.Format("{0:f4}", lettersInfoCount5);
                    
                    //Запись в лог
                    resultTextBox.Text    += "Энтропия при кодировании в 5-ой системе счисления методом Шеннона-Фано: \r\n" +
                    "    Предложения    " + entropySenteces5 + "\r\n" +
                    "    Слова          " + entropyWords5 + "\r\n" +
                    "    Слоги          " + entropySyllables5 + "\r\n" +
                    "    Буквы          " + entropyLetter5 + "\r\n\r\n";
                    resultTextBox.Text    += "Среднее количество элементарных символов на 1 символ текста при кодировании в 5-ой системе счисления методом Шефанно-Фано: \r\n" +
                    "    Предложения    " + sentencesFanoElementaryCount5 + "\r\n" +
                    "    Слова          " + wordsFanoElementaryCount5 + "\r\n" +
                    "    Слоги          " + syllablesFanoElementaryCount5 + "\r\n" +
                    "    Буквы          " + lettersFanoElementaryCount5 + "\r\n\r\n";
                    resultTextBox.Text    += "Количество информации на один элементарный символ при кодировании в 5-ой системе счисления методом Шефанно-Фано: \r\n" +
                    "    Предложения    " + sentencesInfoCount5 + "\r\n" +
                    "    Слова          " + wordsInfoCount5 + "\r\n" +
                    "    Слоги          " + syllablesInfoCount5 + "\r\n" +
                    "    Буквы          " + lettersInfoCount5 + "\r\n\r\n";
                }
                if (Huffmanfor2Check.Checked == true || Huffmanfor3Check.Checked == true || Huffmanfor5Check.Checked == true)
                {
                    CountSentencesHuffmanTextbox.Text   = sentences.Count.ToString();
                    CountUnicSenHufTextbox.Text         = unicsen.ToString();
                    CountWordsHuffmanTextbox.Text       = words.Count.ToString();
                    CountUnicWordsHufTextbox.Text       = unicword.ToString();
                    CountSyllsblesHuffmanTextbox.Text   = syllables.Count.ToString();
                    CountUnicSyllablesHufTextbox.Text   = unicsyllables.ToString();
                    CountLettersHufTextbox.Text         = lettersCountInText.ToString();
                    CountUnicLettersHufTextbox.Text     = unicletter.ToString();
                }
                if (Huffmanfor2Check.Checked == true)
                {
                    // Вывод энтропии при Хаффмене 2 сс
                    entropyHuf2Sen.Text                 = String.Format("{0:f4}", entropySentences2);
                    entropyHuf2W.Text                   = String.Format("{0:f4}", entropyWords);
                    entropyHuf2Syllables.Text           = String.Format("{0:f4}", entropySyllables);
                    entropyHuf2Letters.Text             = String.Format("{0:f4}", entropyLetter);
                    
                    //Вывод количества элементарных символов при Хаффмене 2 сс
                    SenElementaryCountHuf2.Text         = String.Format("{0:f4}", sentencesHuffmanElementaryCount);
                    WordsElementaryCountHuf2.Text       = String.Format("{0:f4}", wordsHuffmanElementaryCount);
                    SyllablElementaryCountHuf2.Text     = String.Format("{0:f4}", syllablesHuffmanElementaryCount);
                    LettersElementaryCountHuf2.Text     = String.Format("{0:f4}", lettersHuffmanElementaryCount);
                    
                    //Вывод количества информации при Хаффмене 2 сс
                    InfoCountSenHuf2.Text               = String.Format("{0:f4}", sentencesInfoHufCount);
                    InfoCountWHuf2.Text                 = String.Format("{0:f4}", wordsInfoHufCount);
                    InfoCountSyllablesHuf2.Text         = String.Format("{0:f4}", syllablesInfoHufCount);
                    InfoCountLettersHuf2.Text           = String.Format("{0:f4}", lettersInfoHufCount);
                    
                    //Запись в лог
                    resultTextBox.Text    += "Энтропия при кодировании в 2-ой системе счисления методом Хаффмена: \r\n" +
                    "    Предложения    " + entropySentences2 + "\r\n" +
                    "    Слова          " + entropyWords + "\r\n" +
                    "    Слоги          " + entropySyllables + "\r\n" +
                    "    Буквы          " + entropyLetter + "\r\n\r\n";
                    resultTextBox.Text    += "Среднее количество элементарных символов на 1 символ текста при кодировании в 2-ой системе счисления методом Хаффмена: \r\n" +
                    "    Предложения    " + sentencesHuffmanElementaryCount + "\r\n" +
                    "    Слова          " + wordsHuffmanElementaryCount + "\r\n" +
                    "    Слоги          " + syllablesHuffmanElementaryCount + "\r\n" +
                    "    Буквы          " + lettersHuffmanElementaryCount + "\r\n\r\n";
                    resultTextBox.Text    += "Количество информации на один элементарный символ при кодировании в 2-ой системе счисления методом Хаффмена: \r\n" +
                    "    Предложения    " + sentencesInfoHufCount + "\r\n" +
                    "    Слова          " + wordsInfoHufCount + "\r\n" +
                    "    Слоги          " + syllablesInfoHufCount + "\r\n" +
                    "    Буквы          " + lettersInfoHufCount + "\r\n\r\n";
                }
                if (Huffmanfor3Check.Checked == true)
                {
                    // Вывод энтропии при Хаффмене 3 сс
                    entropyHuf3Sen.Text                 = String.Format("{0:f4}", entropySenteces3);
                    entropyHuf3W.Text                   = String.Format("{0:f4}", entropyWords3);
                    entropyHuf3Syllables.Text           = String.Format("{0:f4}", entropySyllables3);
                    entropyHuf3Letters.Text             = String.Format("{0:f4}", entropyLetter3);
                    
                    //Вывод количества элементарных символов при Хаффмене 3 сс
                    SenElementaryCountHuf3.Text         = String.Format("{0:f4}", sentencesHuffmanElementaryCount3);
                    WordsElementaryCountHuf3.Text       = String.Format("{0:f4}", wordsHuffmanElementaryCount3);
                    SyllablElementaryCountHuf3.Text     = String.Format("{0:f4}", syllablesHuffmanElementaryCount3);
                    LettersElementaryCountHuf3.Text     = String.Format("{0:f4}", lettersHuffmanElementaryCount3);
                    
                    //Вывод количества информации при Хаффмене 3 сс
                    InfoCountSenHuf3.Text               = String.Format("{0:f4}", sentencesInfoHufCount3);
                    InfoCountWHuf3.Text                 = String.Format("{0:f4}", wordsInfoHufCount3);
                    InfoCountSyllablesHuf3.Text         = String.Format("{0:f4}", syllablesInfoHufCount3);
                    InfoCountLettersHuf3.Text           = String.Format("{0:f4}", lettersInfoHufCount3);
                    
                    //Запись в лог
                    resultTextBox.Text    += "Энтропия при кодировании в 3-ой системе счисления методом Хаффмена: \r\n" +
                    "    Предложения    " + entropySenteces3 + "\r\n" +
                    "    Слова          " + entropyWords3 + "\r\n" +
                    "    Слоги          " + entropySyllables3 + "\r\n" +
                    "    Буквы          " + entropyLetter3 + "\r\n\r\n";
                    resultTextBox.Text    += "Среднее количество элементарных символов на 1 символ текста при кодировании в 3-ой системе счисления методом Хаффмена: \r\n" +
                    "    Предложения    " + sentencesHuffmanElementaryCount3 + "\r\n" +
                    "    Слова          " + wordsHuffmanElementaryCount3 + "\r\n" +
                    "    Слоги          " + syllablesHuffmanElementaryCount3 + "\r\n" +
                    "    Буквы          " + lettersHuffmanElementaryCount3 + "\r\n\r\n";
                    resultTextBox.Text    += "Количество информации на один элементарный символ при кодировании в 3-ой системе счисления методом Хаффмена: \r\n" +
                    "    Предложения    " + sentencesInfoHufCount3 + "\r\n" +
                    "    Слова          " + wordsInfoHufCount3 + "\r\n" +
                    "    Слоги          " + syllablesInfoHufCount3 + "\r\n" +
                    "    Буквы          " + lettersInfoHufCount3 + "\r\n\r\n";
                }
                if (Huffmanfor5Check.Checked == true)
                {
                    // Вывод энтропии при Хаффмене 5 сс
                    entropyHuf5Sen.Text                 = String.Format("{0:f4}", entropySenteces5);
                    entropyHuf5W.Text                   = String.Format("{0:f4}", entropyWords5);
                    entropyHuf5Syllables.Text           = String.Format("{0:f4}", entropySyllables5);
                    entropyHuf5Letters.Text             = String.Format("{0:f4}", entropyLetter5);
                    
                    //Вывод количества элементарных символов при Хаффмене 5 сс
                    SenElementaryCountHuf5.Text         = String.Format("{0:f4}", sentencesHuffmanElementaryCount5);
                    WordsElementaryCountHuf5.Text       = String.Format("{0:f4}", wordsHuffmanElementaryCount5);
                    SyllablElementaryCountHuf5.Text     = String.Format("{0:f4}", syllablesHuffmanElementaryCount5);
                    LettersElementaryCountHuf5.Text     = String.Format("{0:f4}", lettersHuffmanElementaryCount5);
                    
                    //Вывод количества информации при Хаффмене 5 сс
                    InfoCountSenHuf5.Text               = String.Format("{0:f4}", sentencesInfoHufCount5);
                    InfoCountWHuf5.Text                 = String.Format("{0:f4}", wordsInfoHufCount5);
                    InfoCountSyllablesHuf5.Text         = String.Format("{0:f4}", syllablesInfoHufCount5);
                    InfoCountLettersHuf5.Text           = String.Format("{0:f4}", lettersInfoHufCount5);
                    
                    //Запись в лог
                    resultTextBox.Text    += "Энтропия при кодировании в 5-ой системе счисления методом Хаффмена: \r\n" +
                    "    Предложения    " + entropySenteces5 + "\r\n" +
                    "    Слова          " + entropyWords5 + "\r\n" +
                    "    Слоги          " + entropySyllables5 + "\r\n" +
                    "    Буквы          " + entropyLetter5 + "\r\n\r\n";
                    resultTextBox.Text    += "Среднее количество элементарных символов на 1 символ текста при кодировании в 5-ой системе счисления методом Хаффмена: \r\n" +
                    "    Предложения    " + sentencesHuffmanElementaryCount5 + "\r\n" +
                    "    Слова          " + wordsHuffmanElementaryCount5 + "\r\n" +
                    "    Слоги          " + syllablesHuffmanElementaryCount5 + "\r\n" +
                    "    Буквы          " + lettersHuffmanElementaryCount5 + "\r\n\r\n";
                    resultTextBox.Text    += "Количество информации на один элементарный символ при кодировании в 5-ой системе счисления методом Хаффмена: \r\n" +
                    "    Предложения    " + sentencesInfoHufCount5 + "\r\n" +
                    "    Слова          " + wordsInfoHufCount5 + "\r\n" +
                    "    Слоги          " + syllablesInfoHufCount5 + "\r\n" +
                    "    Буквы          " + lettersInfoHufCount5 + "\r\n\r\n";
                }
                if (checkBoxforSen.Checked == true || checkBoxforLetters.Checked == true || checkBoxforSyllables.Checked == true || checkBoxforWords.Checked == true)
                {
                    textBox114.Text = sentences.Count.ToString();
                    textBox113.Text = unicsen.ToString();
                    textBox111.Text = words.Count.ToString();
                    textBox110.Text = unicword.ToString();
                    textBox118.Text = syllables.Count.ToString();
                    textBox116.Text = unicsyllables.ToString();
                    textBox120.Text = lettersCountInText.ToString();
                    textBox119.Text = unicletter.ToString();
                }
                if (checkBoxforSen.Checked == true)
                {
                    // Вывод энтропии при кодировании предложениями
                    textBox50.Text  = String.Format("{0:f4}", entropySentencesSen);
                    textBox47.Text  = "-";
                    textBox54.Text  = "-";
                    textBox56.Text  = "-";

                    //Вывод количества элементарных символов при кодировании предложениями
                    textBox49.Text  = String.Format("{0:f4}", sentencesFanoElementaryCountS);
                    textBox46.Text  = "-"; 
                    textBox52.Text  = "-";
                    textBox55.Text  = "-";

                    //Вывод количества информации при кодировании предложениями
                    textBox48.Text  = String.Format("{0:f4}", sentencesInfoCountS); 
                    textBox45.Text  = "-";
                    textBox51.Text  = "-";
                    textBox53.Text  = "-";

                    //Запись в лог
                    resultTextBox.Text    += "Энтропия при кодировании предложениями: \r\n" +
                    "    Предложения    " + entropySentencesSen + "\r\n\r\n";
                    resultTextBox.Text    += "Среднее количество элементарных символов на 1 символ текста при кодировании предложениями: \r\n" +
                    "    Предложения    " + sentencesFanoElementaryCountS + "\r\n\r\n";
                    resultTextBox.Text    += "Количество информации на один элементарный символ при кодировании предложениями: \r\n" +
                    "    Предложения    " + sentencesInfoCountS + "\r\n\r\n";
                }
                if (checkBoxforWords.Checked == true)
                {
                    // Вывод энтропии при кодировании словами
                    textBox66.Text = String.Format("{0:f4}", entropySentencesW);
                    textBox63.Text = String.Format("{0:f4}", entropyWordsW);
                    textBox70.Text = "-"; 
                    textBox72.Text = "-";

                    //Вывод количества элементарных символов при кодировании словами
                    textBox62.Text = String.Format("{0:f4}", wordsFanoElementaryCountW);
                    textBox65.Text = String.Format("{0:f4}", wordsinsen);
                    textBox68.Text = "-"; 
                    textBox71.Text = "-";

                    //Вывод количества информации при кодировании словами
                    textBox61.Text = String.Format("{0:f4}", wordsInfoCountW);
                    textBox64.Text = String.Format("{0:f4}", sentencesInfoCountW);
                    textBox67.Text = "-";
                    textBox69.Text = "-";

                    //Запись в лог
                    resultTextBox.Text    += "Энтропия при кодировании словами: \r\n" +
                    "    Предложения    " + entropySentencesW + "\r\n" +
                    "    Слова          " + entropyWordsW + "\r\n\r\n";
                    resultTextBox.Text    += "Среднее количество элементарных символов на 1 символ текста при кодировании словами: \r\n" +
                    "    Предложения    " + wordsinsen + "\r\n" +
                    "    Слова          " + wordsFanoElementaryCountW + "\r\n\r\n";
                    resultTextBox.Text    += "Количество информации на один элементарный символ при кодировании словами: \r\n" +
                    "    Предложения    " + sentencesInfoCountW + "\r\n" +
                    "    Слова          " + wordsInfoCountW + "\r\n\r\n";
                }
                if (checkBoxforSyllables.Checked == true)
                {
                    // Вывод энтропии при кодировании слогами
                    textBox18.Text = String.Format("{0:f4}", entropySentecesS);
                    textBox15.Text = String.Format("{0:f4}", entropyWordsS);
                    textBox58.Text = String.Format("{0:f4}", entropyS);
                    textBox60.Text = "-";

                    //Вывод количества элементарных символов при кодировании слогами
                    textBox20.Text = String.Format("{0:f4}", syllablesFanoElementaryCountS);
                    textBox17.Text = String.Format("{0:f4}", syllablesinsen);
                    textBox14.Text = String.Format("{0:f4}", syllablesinwords);
                    textBox59.Text = "-";

                    //Вывод количества информации при кодировании слогами
                    textBox19.Text = String.Format("{0:f4}", syllablesInfoCountS);
                    textBox16.Text = String.Format("{0:f4}", sentencesInfoCountSyl);
                    textBox13.Text = String.Format("{0:f4}", wordsInfoCountS);
                    textBox57.Text = "-";

                    //Запись в лог
                    resultTextBox.Text    += "Энтропия при кодировании слогами: \r\n" +
                    "    Предложения    " + entropySentecesS + "\r\n" +
                    "    Слова          " + entropyWordsS + "\r\n" +
                    "    Слоги          " + entropyS + "\r\n\r\n";
                    resultTextBox.Text    += "Среднее количество элементарных символов на 1 символ текста при кодировании слогами: \r\n" +
                    "    Предложения    " + syllablesinsen + "\r\n" +
                    "    Слова          " + syllablesinwords + "\r\n" +
                    "    Слоги          " + syllablesFanoElementaryCountS + "\r\n\r\n";
                    resultTextBox.Text    += "Количество информации на один элементарный символпри кодировании слогами: \r\n" +
                    "    Предложения    " + sentencesInfoCountSyl + "\r\n" +
                    "    Слова          " + wordsInfoCountS + "\r\n" +
                    "    Слоги          " + syllablesInfoCountS + "\r\n\r\n";
                }
                if (checkBoxforLetters.Checked == true)
                {
                    // Вывод энтропии при кодировании буквами
                    textBox6.Text   = String.Format("{0:f4}", entropySentecesL);
                    textBox3.Text   = String.Format("{0:f4}", entropyWordsL);
                    textBox10.Text  = String.Format("{0:f4}", entropySyllablesL);
                    textBox12.Text  = String.Format("{0:f4}", entropyLetterL);

                    //Вывод количества элементарных символов при кодировании буквами
                    textBox11.Text  = String.Format("{0:f4}", lettersFanoElementaryCountL);
                    textBox5.Text   = String.Format("{0:f4}", letterinsen);
                    textBox2.Text   = String.Format("{0:f4}", letterinwords);
                    textBox8.Text   = String.Format("{0:f4}", letterinsyllables);

                    //Вывод количества информации при кодировании буквами
                    textBox4.Text   = String.Format("{0:f4}", sentencesInfoCountL);
                    textBox1.Text   = String.Format("{0:f4}", wordsInfoCountL);
                    textBox7.Text   = String.Format("{0:f4}", syllablesInfoCountL);
                    textBox9.Text   = String.Format("{0:f4}", lettersInfoCountL);

                    //Запись в лог
                    resultTextBox.Text    += "Энтропия при кодировании буквами алфавита: \r\n" +
                    "    Предложения    " + entropySentecesL + "\r\n" +
                    "    Слова          " + entropyWordsL + "\r\n" +
                    "    Слоги          " + entropySyllablesL + "\r\n" +
                    "    Буквы          " + entropyLetterL + "\r\n\r\n";
                    resultTextBox.Text    += "Среднее количество элементарных символов на 1 символ текста при кодировании буквами алфавита: \r\n" +
                    "    Предложения    " + letterinsen + "\r\n" +
                    "    Слова          " + letterinwords + "\r\n" +
                    "    Слоги          " + letterinsyllables + "\r\n" +
                    "    Буквы          " + lettersFanoElementaryCountL + "\r\n\r\n";
                    resultTextBox.Text    += "Количество информации на один элементарный символ при кодировании буквами алфавита: \r\n" +
                    "    Предложения    " + sentencesInfoCountL + "\r\n" +
                    "    Слова          " + wordsInfoCountL + "\r\n" +
                    "    Слоги          " + syllablesInfoCountL + "\r\n" +
                    "    Буквы          " + lettersInfoCountL + "\r\n\r\n";
                }

                Directory.CreateDirectory(folderPathDialog + "\\" + dateTime);
                folderPath = folderPathDialog + "\\" + dateTime;

                resultTextBox.Text += "Завершено";
            }
            if (radioButton2.Checked)
            {
                /*string[] sentencesSplit = Regex.Split(text, @"(?<=[\.!\?{:\r}])\s+");
                int sc = sentencesSplit.Count();
                List<string> sentences = new List<string>();
                for (int i = 0; i < sc; i++)
                {
                    string firstSymbol = sentencesSplit[i][0].ToString();
                    if (firstSymbol == firstSymbol.ToLower() && i > 0 && alphabetru.Contains(firstSymbol.ToLower()))
                    {
                        string lastCentences = sentences.Last();
                        sentences.RemoveAt(sentences.Count - 1);
                        sentences.Add(lastCentences + " " + sentencesSplit[i]);
                        continue;
                    }
                    sentences.Add(sentencesSplit[i]);
                }
                resultTextBox.Text += "Step 1. Tokenization. Removing extra characters\r\n";

                var words = text.ToLower().Split(separator, StringSplitOptions.RemoveEmptyEntries).ToList();

                resultTextBox.Text += "Characters:" + GetLettersCountInText(words) + "\r\n";
                resultTextBox.Text += "Words: " + words.Count + "\r\n\r\n";

                resultTextBox.Text += "Step 2. Stop list. Removing words from the stop list and words containing non-English alphabet characters\r\n";

                words.RemoveAll(word => stopWordseng.Any(stopWord => word == stopWord) || CheckLetterEng(word));
                if (words.Count == 0)
                {
                    resultTextBox.Text += "The program is stopped, the number of words is 0\r\n";
                    return;
                }

                resultTextBox.Text += "Words: " + words.Count + "\r\n\r\n";
                resultTextBox.Text += "Step 3. Division into syllables\r\n";

                var syllables = new List<string>();
                words.ForEach(word => GetSyllablesEng(word).ToList().ForEach(el => syllables.Add(el)));
                resultTextBox.Text += "Syllables: " + syllables.Count + "\r\n\r\n";

                int lettersCountInText = GetLettersCountInText(words);

                //встречаемость букв+частота
                var letterCountDict = GetLetterCount(words).OrderByDescending(pair => pair.Value).ToDictionary(pair => pair.Key, pair => pair.Value);
                var probabilityLetter = GetProbability(letterCountDict, lettersCountInText).OrderByDescending(pair => pair.Value).ToDictionary(pair => pair.Key, pair => pair.Value);

                //встречаемость слогов+частота
                var syllablesCountDict = syllables.GroupBy(el => el).ToDictionary(el => el.Key, el => el.Count()).OrderByDescending(pair => pair.Value).ToDictionary(pair => pair.Key, pair => pair.Value);
                var probabilitySyllables = GetProbability(syllablesCountDict, syllables.Count).OrderByDescending(pair => pair.Value).ToDictionary(pair => pair.Key, pair => pair.Value);

                //Встречаемость предложений+частота
                var sentencesCountDict = sentences.GroupBy(el => el).ToDictionary(el => el.Key, el => el.Count()).OrderByDescending(pair => pair.Value).ToDictionary(pair => pair.Key, pair => pair.Value);
                var probabilitySentences = GetProbability(sentencesCountDict, sentences.Count).OrderByDescending(pair => pair.Value).ToDictionary(pair => pair.Key, pair => pair.Value);

                //Встречаемость слов + частота
                var wordsCountDict = words.GroupBy(el => el).ToDictionary(el => el.Key, el => el.Count()).OrderByDescending(pair => pair.Value).ToDictionary(pair => pair.Key, pair => pair.Value);
                var probabilityWords = GetProbability(wordsCountDict, words.Count).OrderByDescending(pair => pair.Value).ToDictionary(pair => pair.Key, pair => pair.Value);

                //Энтропия предложений
                var entropySentences2 = GetEntropy(probabilitySentences);//расчет энтропии предложений при кодировании 2сс
                double unicword = wordsCountDict.Count;//количество уникальных слов
                double unicsyllables = syllablesCountDict.Count;//количество уникальных слогов
                double unicletter = letterCountDict.Count;//количество букв алфавита
                double unicsen = sentencesCountDict.Count;//количество уникальных предложений
                countSentencesTextBox.Text = sentences.Count.ToString();
                textBox162.Text = sentences.Count.ToString();//вывод количество предложений всего
                entropySentencesTextBox.Text = String.Format("{0:f4}", entropySentences2);//вывод энтропия предложений при кодировании 2 сс
                countSentencesUnicTextBox.Text = unicsen.ToString();
                textBox153.Text = unicsen.ToString();//вывод количества уникальных предложений
                var entropySenteces3 = GetEntropy3(probabilitySentences);//расчет энтропии предложений при кодировании 3 сс
                var entropySenteces5 = GetEntropy5(probabilitySentences);//расчет энтропии предложений при кодировании 5 сс
                var entropySentencesSen = GetEntropyLec(probabilitySentences, unicsen);//расчет энтропии предложений при кодировании предложениями
                var entropySentencesW = GetEntropyLec(probabilitySentences, unicword);//расчет энтропии предложений при кодировании словами
                var entropySentecesS = GetEntropyLec(probabilitySentences, unicsyllables);//расчет энтропии предложений при кодировании слогами
                var entropySentecesL = GetEntropyLec(probabilitySentences, unicletter);//расчет энтропии предложений при кодировании буквами
                textBox26.Text = String.Format("{0:f4}", entropySenteces3);//вывод энтропии при 3сс
                textBox38.Text = String.Format("{0:f4}", entropySenteces5);//вывод энтропии при 5сс
                textBox50.Text = String.Format("{0:f4}", entropySentencesSen);//вывод энтропии при предложениях
                textBox66.Text = String.Format("{0:f4}", entropySentencesW);//вывод энтропии при словах
                textBox18.Text = String.Format("{0:f4}", entropySentecesS);//вывод энтропии при слогах
                textBox6.Text = String.Format("{0:f4}", entropySentecesL);//вывод энтропии при буквах

                //Энтропия слов
                var entropyWords = GetEntropy(probabilityWords);//расчет энтропии при кодировании 2сс
                countWordsTextBox.Text = words.Count.ToString();
                textBox154.Text = words.Count.ToString();//вывод количества слов
                countWordsUnicTextBox.Text = unicword.ToString();
                textBox148.Text = unicword.ToString();//вывод количества уникальных слов
                entropyWordsTextBox.Text = String.Format("{0:f4}", entropyWords);//вывод энтропии при кодировании 2сс
                var entropyWords3 = GetEntropy3(probabilityWords);//расчет энтропии при кодировании 3сс
                var entropyWords5 = GetEntropy5(probabilityWords);//расчет энтропии при кодировании 5сс
                var entropyWordsS = GetEntropyLec(probabilityWords, unicsyllables);//расчет энтропии слов при кодировании слогамми
                var entropyWordsL = GetEntropyLec(probabilityWords, unicletter);//расчет энтропии слов при кодировании буквами
                var entropyWordsW = GetEntropyLec(probabilityWords, unicword);//расчет энтропии слов при кодировании словами
                textBox23.Text = String.Format("{0:f4}", entropyWords3);//вывод энтропии при 3сс
                textBox35.Text = String.Format("{0:f4}", entropyWords5);//вывод энтропии при 5сс
                textBox63.Text = String.Format("{0:f4}", entropyWordsW);//вывод энтропии при словах
                textBox15.Text = String.Format("{0:f4}", entropyWordsS);//вывод энтропии при слогах
                textBox3.Text = String.Format("{0:f4}", entropyWordsL);//вывод энропии при буквах
                textBox47.Text = "-"; textBox111.Text = "-"; textBox46.Text = "-"; textBox110.Text = "-"; textBox45.Text = "-"; //заполнение энтропии слов которые не расчитываются

                //Энтропия слогов
                var entropyS = GetEntropyLec(probabilitySyllables, unicsyllables);//расчет энтропии слогов при кодировании слогами
                var entropySyllablesL = GetEntropyLec(probabilitySyllables, unicletter);//расчет энтропии слогов при кодировании буквами
                var entropySyllables = GetEntropy(probabilitySyllables);//расчет энтропии при кодировании 2сс
                var entropySyllables3 = GetEntropy3(probabilitySyllables);//расчет энтропии при кодировании 3сс
                var entropySyllables5 = GetEntropy5(probabilitySyllables);//расчет энтропии при кодировании 5сс
                countSyllablesTextBox.Text = syllables.Count.ToString();
                textBox164.Text = syllables.Count.ToString();//вывод количества символов
                countSyllablesUnicTextBox.Text = unicsyllables.ToString();
                textBox161.Text = unicsyllables.ToString();//вывод количества уникальных символов
                entropySyllablesTextBox.Text = String.Format("{0:f4}", entropySyllables);//вывод энтропии слогов при кодировании 2сс
                textBox30.Text = String.Format("{0:f4}", entropySyllables3);//вывод энтропии при 3сс
                textBox42.Text = String.Format("{0:f4}", entropySyllables5);//вывод энтропии при 5сс
                textBox58.Text = String.Format("{0:f4}", entropyS);//вывод энтропии слогов при кодировании слогами
                textBox10.Text = String.Format("{0:f4}", entropySyllablesL);//вывод энтропии слогов при кодировании буквами
                textBox54.Text = "-"; textBox118.Text = "-"; textBox52.Text = "-"; textBox116.Text = "-"; textBox51.Text = "-"; 
                textBox70.Text = "-"; textBox68.Text = "-";  textBox67.Text = "-"; textBox103.Text = "-";//заполнение энтропии слогов которые не расчитывались

                //Энтропия букв
                var entropyLetter = GetEntropy(probabilityLetter);//расчет энтропии букв при кодировании 2сс
                var entropyLetter3 = GetEntropy3(probabilityLetter);//расчет энтропии при кодировании 3сс
                var entropyLetter5 = GetEntropy5(probabilityLetter);//расчет энтропии при кодировании 5сс
                var entropyLetterL = GetEntropyLec(probabilityLetter, unicletter);//расчет энтропии при кодировании буквами
                countLetterTextBox.Text = lettersCountInText.ToString();
                textBox149.Text = lettersCountInText.ToString();//вывод количества букв
                countLetterUnicTextBox.Text = unicletter.ToString();
                textBox163.Text = unicletter.ToString();//вывод количества уникальных букв
                entropyLetterTextBox.Text = String.Format("{0:f4}", entropyLetter);//вывод энтропии при 2сс
                textBox32.Text = String.Format("{0:f4}", entropyLetter3);//вывод энтропии при 3сс
                textBox44.Text = String.Format("{0:f4}", entropyLetter5);//вывод энтропии при 5сс
                textBox12.Text = String.Format("{0:f4}", entropyLetterL);//вывод энтропии при буквах
                textBox56.Text = "-"; textBox120.Text = "-"; textBox55.Text = "-"; textBox119.Text = "-"; textBox53.Text = "-"; 
                textBox72.Text = "-"; textBox108.Text = "-"; textBox71.Text = "-"; textBox107.Text = "-"; textBox69.Text = "-"; textBox105.Text = "-";
                textBox60.Text = "-"; textBox96.Text = "-"; textBox59.Text = "-"; textBox95.Text = "-"; textBox57.Text = "-"; textBox93.Text = "-";//заполнение энтропии букв которые не расчитываются

                resultTextBox.Text += "Step 4. Calculations\r\n";


                //Фано
                string[] sentencesFano = new string[sentencesCountDict.Count];

                string[] wordsFano = new string[wordsCountDict.Count];
                string[] syllablesFano = new string[syllablesCountDict.Count];
                string[] lettersFano = new string[letterCountDict.Count];

                Fano(0, sentencesCountDict.Count - 1, sentencesFano, probabilitySentences);
                Fano(0, wordsCountDict.Count - 1, wordsFano, probabilityWords);
                Fano(0, syllablesCountDict.Count - 1, syllablesFano, probabilitySyllables);
                Fano(0, letterCountDict.Count - 1, lettersFano, probabilityLetter);

                //среднее количество элементраных символов
                double sentencesFanoElementaryCount = CountElementaryCharOnOneEncodeChar(sentencesFano, probabilitySentences);
                double wordsFanoElementaryCount = CountElementaryCharOnOneEncodeChar(wordsFano, probabilityWords);
                double syllablesFanoElementaryCount = CountElementaryCharOnOneEncodeChar(syllablesFano, probabilitySyllables);
                double lettersFanoElementaryCount = CountElementaryCharOnOneEncodeChar(lettersFano, probabilityLetter);

                SentencesElementaryTextBox.Text = String.Format("{0:f4}", sentencesFanoElementaryCount);
                WordsElementaryTextBox.Text = String.Format("{0:f4}", wordsFanoElementaryCount);
                SyllablesElementaryTextBox.Text = String.Format("{0:f4}", syllablesFanoElementaryCount);
                LettersElementaryTextBox.Text = String.Format("{0:f4}", lettersFanoElementaryCount);

                //Кол-в информации на 1 симол
                double sentencesInfoCount = GetInfoCount(entropySentences2, sentencesFanoElementaryCount);
                double wordsInfoCount = GetInfoCount(entropyWords, wordsFanoElementaryCount);
                double syllablesInfoCount = GetInfoCount(entropySyllables, syllablesFanoElementaryCount);
                double lettersInfoCount = GetInfoCount(entropyLetter, lettersFanoElementaryCount);

                sentencesInfoCountTextBox.Text = String.Format("{0:f4}", sentencesInfoCount);
                wordsInfoCountTextBox.Text = String.Format("{0:f4}", wordsInfoCount);
                syllablesInfoCountTextBox.Text = String.Format("{0:f4}", syllablesInfoCount);
                lettersInfoCountTextBox.Text = String.Format("{0:f4}", lettersInfoCount);

                Directory.CreateDirectory(folderPathDialog + "\\" + dateTime);
                folderPath = folderPathDialog + "\\" + dateTime;

                WriteToFileCVS("Предложения", sentencesCountDict, probabilitySentences, sentences.Count, entropySentences2, sentencesFano, sentencesFanoElementaryCount, sentencesInfoCount);
                WriteToFileCVS("Слова", wordsCountDict, probabilityWords, words.Count, entropyWords, wordsFano, wordsFanoElementaryCount, wordsInfoCount);
                WriteToFileCVS("Слоги", syllablesCountDict, probabilitySyllables, syllables.Count, entropySyllables, syllablesFano, syllablesFanoElementaryCount, syllablesInfoCount);
                WriteToFileCVS("Буквы", letterCountDict, probabilityLetter, lettersCountInText, entropyLetter, lettersFano, lettersFanoElementaryCount, lettersInfoCount);

                resultTextBox.Text += "Количество символов, всего в тексте: \r\n" +
                   "    Предложения    " + countSentencesTextBox.Text + "\r\n" +
                   "    Слова          " + countWordsTextBox.Text + "\r\n" +
                   "    Слоги          " + countSyllablesTextBox.Text + "\r\n" +
                   "    Буквы          " + countLetterTextBox.Text + "\r\n\r\n";

                resultTextBox.Text += "Количество уникальных символов: \r\n" +
                    "    Предложения    " + countSentencesUnicTextBox.Text + "\r\n" +
                    "    Слова          " + countWordsUnicTextBox.Text + "\r\n" +
                    "    Слоги          " + countSyllablesUnicTextBox.Text + "\r\n" +
                    "    Буквы          " + countLetterUnicTextBox.Text + "\r\n\r\n";

                resultTextBox.Text += "Энтропия: \r\n" +
                    "    Предложения    " + entropySentences2 + "\r\n" +
                    "    Слова          " + entropyWords + "\r\n" +
                    "    Слоги          " + entropySyllables + "\r\n" +
                    "    Буквы          " + entropyLetter + "\r\n\r\n";

                resultTextBox.Text += "Среднее количество элементарных символов на 1 символ текста: \r\n" +
                    "    Предложения    " + sentencesFanoElementaryCount + "\r\n" +
                    "    Слова          " + wordsFanoElementaryCount + "\r\n" +
                    "    Слоги          " + syllablesFanoElementaryCount + "\r\n" +
                    "    Буквы          " + lettersFanoElementaryCount + "\r\n\r\n";

                resultTextBox.Text += "Количество информации на один элементарный символ: \r\n" +
                    "    Предложения    " + sentencesInfoCount + "\r\n" +
                    "    Слова          " + wordsInfoCount + "\r\n" +
                    "    Слоги          " + syllablesInfoCount + "\r\n" +
                    "    Буквы          " + lettersInfoCount + "\r\n\r\n";
                resultTextBox.Text += "Завершено";*/
            }
            WriteToFile("Вывод двоичное кодирование Фано", "Путь к файлу: " + textFilePath + "\r\n" + resultTextBox.Text);
        }

        private void checkBox13_CheckedChanged(object sender, EventArgs e)
        {
            switch (allFano.CheckState)
            {
                case CheckState.Checked:
                    {
                        Fanofor2Check.Checked = true;
                        Fanofor3Check.Checked = true;
                        Fanofor5Check.Checked = true;
                        break;
                    }
                case CheckState.Unchecked:
                    {
                        Fanofor2Check.Checked = false;
                        Fanofor3Check.Checked = false;
                        Fanofor5Check.Checked = false;
                        break;
                    }
            }
        }

        private void checkBox14_CheckedChanged(object sender, EventArgs e)
        {
            switch (allHuffman.CheckState)
            {
                case CheckState.Checked:
                    {
                        Huffmanfor2Check.Checked = true;
                        Huffmanfor3Check.Checked = true;
                        Huffmanfor5Check.Checked = true;
                        break;
                    }
                case CheckState.Unchecked:
                    {
                        Huffmanfor2Check.Checked = false;
                        Huffmanfor3Check.Checked = false;
                        Huffmanfor5Check.Checked = false;
                        break;
                    }
            }
        }

        private void checkBox15_CheckedChanged(object sender, EventArgs e)
        {
            switch (allLec.CheckState)
            {
                case CheckState.Checked:
                    {
                        checkBoxforLetters.Checked = true;
                        checkBoxforSen.Checked = true;
                        checkBoxforWords.Checked = true;
                        checkBoxforSyllables.Checked = true;
                        break;
                    }
                case CheckState.Unchecked:
                    {
                        checkBoxforLetters.Checked = false;
                        checkBoxforSen.Checked = false;
                        checkBoxforWords.Checked = false;
                        checkBoxforSyllables.Checked = false;
                        break;
                    }
            }
        }

        private void checkBox11_CheckedChanged(object sender, EventArgs e)
        {
            switch (all.CheckState)
            {
                case CheckState.Checked:
                    {
                        Fanofor2Check.Checked = true;
                        checkBoxforLetters.Checked = true;
                        allFano.Checked = true;
                        allHuffman.Checked = true;
                        allLec.Checked = true;
                        Huffmanfor2Check.Checked = true;
                        Fanofor3Check.Checked = true;
                        Huffmanfor3Check.Checked = true;
                        Fanofor5Check.Checked = true;
                        Huffmanfor5Check.Checked = true;
                        checkBoxforSen.Checked = true;
                        checkBoxforWords.Checked = true;
                        checkBoxforSyllables.Checked = true;
                        break;
                    }
                case CheckState.Unchecked:
                    {
                        Fanofor2Check.Checked = false;
                        checkBoxforLetters.Checked = false;
                        allFano.Checked = false;
                        allHuffman.Checked = false;
                        allLec.Checked = false;
                        Huffmanfor2Check.Checked = false;
                        Fanofor3Check.Checked = false;
                        Huffmanfor3Check.Checked = false;
                        Fanofor5Check.Checked = false;
                        Huffmanfor5Check.Checked = false;
                        checkBoxforSen.Checked = false;
                        checkBoxforWords.Checked = false;
                        checkBoxforSyllables.Checked = false;
                        break;
                    }
            }
        }
        private double GetInfoCount(double entropy, double elementaryCount)
        {
            return entropy / elementaryCount;
        }

        //Расчет среднего количества элементарных символов
        private double CountElementaryCharOnOneEncodeChar<T>(string[] fano, Dictionary<T, double> dict)
        {
            double sum = 0;
            for (int i = 0; i < dict.Count; i++)
                sum += dict.ElementAt(i).Value * fano[i].Length;
            return sum;
        }

        //Расчет количества информации на единицу при кодировании предложениями и слогами
        private int CountSyllablesinlec(string word)
        {
            var syllable = new StringBuilder();
            int n = 0;
            bool case1(int index) => word[index] == 'й' && Soglasru.Contains(word[index + 1]);
            bool case2(int index) => Zvonk.Contains(word[index]) && Gluh.Contains(word[index + 1]);
            int i = 0;
            for (; GlasLeft(word, i) > 1 || syllable.Length != 0; i++)
            {
                syllable.Append(word[i]);
                if (case1(i) ||
                    case2(i) ||
                    Glasru.Contains(word[i]) && !(case1(i + 1) || case2(i + 1)))
                {
                    n++;
                    syllable.Clear();
                }
            }
            return n;
        }

        private double CountElementaryLet(Dictionary<string, double> dict)
        {
            double sum = 0;
            for (int i = 0; i < dict.Count; i++)
                sum += dict.ElementAt(i).Value * CountLetters(dict.ElementAt(i).Key);
            return sum;
        }

        private int CountLetters(string input)
        {
            int count = 0;
            for (int j = 0; j < input.Length; j++)
                if (alphabetru.Contains(input[j]))
                    count++;
            return count;
        }

        private double CountElementaryWord(Dictionary<string, double> dict)
        {
            double sum = 0;
            for (int i = 0; i < dict.Count; i++)
                sum += dict.ElementAt(i).Value * CountWords(dict.ElementAt(i).Key);
            return sum;
        }

        private int CountWords(string L)
        {
            int c;
            string[] k;
            k = L.Split(' ');
            c = k.Length;
            return c;
        }

        private double CountElementaryLec(Dictionary<char, double> P1)
        {
            double sum = 0;
            for (int i = 0; i < P1.Count; i++)
                sum += P1.ElementAt(i).Value;
            return sum;
        }

        private double CountElementarySentences(Dictionary<string, double> P1)
        {
            double sum = 0;
            for (int i = 0; i < P1.Count; i++)
                sum += P1.ElementAt(i).Value;
            return sum;
        }

        private double CountElementarySylla(Dictionary<string, double> dict)
        {
            double sum = 0;
            for (int i = 0; i < dict.Count; i++)
                sum += dict.ElementAt(i).Value * CountSyllablesinlec(dict.ElementAt(i).Key);
            return sum;
        }

        //Запись результатов в отчет Excel
        private void WriteToFileCVS<T>(string filename, Dictionary<T, int> dict, Dictionary<T, double> pdict, int count, double entropy, string[] fano, double elementaryCount, double infoCount)
        {
            string path;
            path = folderPath + "\\" + filename + " " + dateTime + ".csv";
            var writer = new StreamWriter(path, false, Encoding.UTF8);
            var csvWriter = new CsvWriter(writer, CultureInfo.CurrentCulture);

            csvWriter.WriteField(filename);
            csvWriter.WriteField("Встречаемость");
            csvWriter.WriteField("Вероятность");
            csvWriter.WriteField("Фано");
            csvWriter.WriteField("Количество всего");
            csvWriter.WriteField(count);
            csvWriter.WriteField("Энтропия");
            csvWriter.WriteField(entropy);
            csvWriter.WriteField("Ср. кол-во эл. симв.");
            csvWriter.WriteField(elementaryCount);
            csvWriter.WriteField("Кол-во инф. на 1 симв.");
            csvWriter.WriteField(infoCount);
            csvWriter.NextRecord();

            for (int i = 0; i < dict.Count; i++)
            {
                csvWriter.WriteField(dict.ElementAt(i).Key);
                csvWriter.WriteField(dict.ElementAt(i).Value);
                csvWriter.WriteField(pdict.ElementAt(i).Value);
                csvWriter.WriteField("'" + fano[i]);
                csvWriter.NextRecord();
            }

            writer.Flush();
            writer.Close();
        }

        private void WriteToFileCVS2<T>(string filename, Dictionary<T, int> dict, Dictionary<T, double> pdict, int count, double entropy, double elementaryCount, double infoCount)
        {
            string path;
            path = folderPath + "\\" + filename + " " + dateTime + ".csv";
            var writer = new StreamWriter(path, false, Encoding.UTF8);
            var csvWriter = new CsvWriter(writer, CultureInfo.CurrentCulture);

            csvWriter.WriteField(filename);
            csvWriter.WriteField("Встречаемость");
            csvWriter.WriteField("Вероятность");
            csvWriter.WriteField("Фано");
            csvWriter.WriteField("Количество всего");
            csvWriter.WriteField(count);
            csvWriter.WriteField("Энтропия");
            csvWriter.WriteField(entropy);
            csvWriter.WriteField("Ср. кол-во эл. симв.");
            csvWriter.WriteField(elementaryCount);
            csvWriter.WriteField("Кол-во инф. на 1 симв.");
            csvWriter.WriteField(infoCount);
            csvWriter.NextRecord();

            for (int i = 0; i < dict.Count; i++)
            {
                csvWriter.WriteField(dict.ElementAt(i).Key);
                csvWriter.WriteField(dict.ElementAt(i).Value);
                csvWriter.WriteField(pdict.ElementAt(i).Value);
                csvWriter.NextRecord();
            }

            writer.Flush();
            writer.Close();
        }

        private void WriteToFile(string fileName, string text)
        {
            string path = folderPath + "\\" + fileName + " " + dateTime + ".txt";

            // запись в файл
            using (FileStream fstream = new FileStream(path, FileMode.Create))
            {
                // преобразуем строку в байты
                byte[] array = System.Text.Encoding.Default.GetBytes(text);
                // запись массива байтов в файл
                fstream.Write(array, 0, array.Length);
            }
        }

        //Проверки
        private bool CheckLetterRu(string word)
        {
            foreach (char letter in word)
            {
                if (!alphabetru.Contains(letter))
                    return true;
            }
            return false;
        }

        private bool CheckLetterEng(string word)
        {
            foreach (char letter in word)
            {
                if (!alphabeteng.Contains(letter))
                    return true;
            }
            return false;
        }

        private int GetLettersCountInText(List<string> words)
        {
            int count = 0;
            words.ForEach(el => count += el.Length);
            return count;
        }

        //Словари
        private Dictionary<string, int> GetLetterCountInStringFormat(Dictionary<char, int> letter)
        {
            Dictionary<string, int> LetterCountString = new Dictionary<string, int>();
            foreach (KeyValuePair<char, int> valuePair in letter)
            {
                LetterCountString.Add(valuePair.Key.ToString(), valuePair.Value);
            }
            return LetterCountString;
        }

        private Dictionary<char, int> GetLetterCount(List<string> words)
        {
            if (radioButton1.Checked)
            {
                Dictionary<char, int> letterCountDict = new Dictionary<char, int> { ['а'] = 0, ['б'] = 0, ['в'] = 0, ['г'] = 0, ['д'] = 0, ['е'] = 0, ['ё'] = 0, ['ж'] = 0, ['з'] = 0, ['и'] = 0, ['й'] = 0, ['к'] = 0, ['л'] = 0, ['м'] = 0, ['н'] = 0, ['о'] = 0, ['п'] = 0, ['р'] = 0, ['с'] = 0, ['т'] = 0, ['у'] = 0, ['ф'] = 0, ['х'] = 0, ['ц'] = 0, ['ч'] = 0, ['ш'] = 0, ['щ'] = 0, ['ъ'] = 0, ['ы'] = 0, ['ь'] = 0, ['э'] = 0, ['ю'] = 0, ['я'] = 0 };
                foreach (string word in words)
                {
                    foreach (char w in word)
                    {
                        letterCountDict[w] += 1;
                    }
                }
                return letterCountDict.Where(item => item.Value != 0).ToDictionary(item => item.Key, item => item.Value);
            }
            else
            {
                Dictionary<char, int> letterCountDict = new Dictionary<char, int> { ['a'] = 0, ['b'] = 0, ['c'] = 0, ['q'] = 0, ['w'] = 0, ['e'] = 0, ['r'] = 0, ['t'] = 0, ['y'] = 0, ['u'] = 0, ['i'] = 0, ['o'] = 0, ['p'] = 0, ['s'] = 0, ['d'] = 0, ['f'] = 0, ['g'] = 0, ['h'] = 0, ['j'] = 0, ['k'] = 0, ['l'] = 0, ['z'] = 0, ['x'] = 0, ['v'] = 0, ['n'] = 0, ['m'] = 0 };
                foreach (string word in words)
                {
                    foreach (char w in word)
                    {
                        letterCountDict[w] += 1;
                    }
                }
                return letterCountDict.Where(item => item.Value != 0).ToDictionary(item => item.Key, item => item.Value);
            }
        }

        //Получение вероятностей
        private static Dictionary<string, double> GetProbability(Dictionary<string, int> dict, int count)
        {
            var result = new Dictionary<string, double>();
            foreach (KeyValuePair<string, int> keyValue in dict)
            {
                result[keyValue.Key] = (double)keyValue.Value / count;
            }
            return result;
        }

        private static Dictionary<char, double> GetProbability(Dictionary<char, int> dict, int count)
        {
            var result = new Dictionary<char, double>();
            foreach (KeyValuePair<char, int> keyValue in dict)
            {
                result[keyValue.Key] = (double)keyValue.Value / count;
            }
            return result;
        }

        //Выбор файлов
        private void ButtonOpenFile_Click(object sender, EventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "Text files (*.txt)|*.txt"
            };
            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                FilePathTextBox.Text = openFileDialog.FileName;
                textFilePath = openFileDialog.FileName;
            }
            ClearInfo();
        }

        private void ButtonOpenFolder_Click(object sender, EventArgs e)
        {
            FolderBrowserDialog folderBrowserDialog = new FolderBrowserDialog();
            if (folderBrowserDialog.ShowDialog() == DialogResult.OK)
            {
                FolderPathTextBox.Text = folderBrowserDialog.SelectedPath;
                folderPathDialog = folderBrowserDialog.SelectedPath;
            }
            ClearInfo();
        }

        //Расчет энтропии
        private static double GetEntropy<T>(Dictionary<T, double> dict)
        {
            double sum = 0;
            foreach (KeyValuePair<T, double> keyValue in dict)
            {
                if (keyValue.Value != 0)
                    sum += keyValue.Value * Math.Log(keyValue.Value, 2);
            }
            return -sum;
        }

        private static double GetEntropy3<T>(Dictionary<T, double> dict)
        {
            double sum = 0;
            foreach (KeyValuePair<T, double> keyValue in dict)
            {
                if (keyValue.Value != 0)
                    sum += keyValue.Value * Math.Log(keyValue.Value, 3);
            }
            return -sum;
        }

        private static double GetEntropy5<T>(Dictionary<T, double> dict)
        {
            double sum = 0;
            foreach (KeyValuePair<T, double> keyValue in dict)
            {
                if (keyValue.Value != 0)
                    sum += keyValue.Value * Math.Log(keyValue.Value, 5);
            }
            return -sum;
        }

        private static double GetEntropyLec<T>(Dictionary<T, double> dict, double a)
        {
            double sum = 0;
            foreach (KeyValuePair<T, double> keyValue in dict)
            {
                if (keyValue.Value != 0)
                    sum += keyValue.Value * Math.Log(keyValue.Value, a);
            }
            return -sum;
        }

        //Деление на слога
        private IEnumerable<string> GetSyllablesRu(string word)
        {
            var syllable = new StringBuilder();
            bool case1(int index) => word[index] == 'й' && Soglasru.Contains(word[index + 1]);
            bool case2(int index) => Zvonk.Contains(word[index]) && Gluh.Contains(word[index + 1]);
            int i = 0;
            for (; GlasLeft(word, i) > 1 || syllable.Length != 0; i++)
            {
                syllable.Append(word[i]);
                if (case1(i) ||
                    case2(i) ||
                    Glasru.Contains(word[i]) && !(case1(i + 1) ||
                    case2(i + 1)))
                {
                    yield return syllable.ToString();
                    syllable.Clear();
                }
            }
            yield return word.Substring(i);
        }

        private IEnumerable<string> GetSyllablesEng(string word)
        {
            var syllable = new StringBuilder();
            int i = 0;
            for (; GlasLeft(word, i) > 1 || syllable.Length != 0; i++)
            {
                syllable.Append(word[i]);
                if (Glaseng.Contains(word[i]))
                {
                    yield return syllable.ToString();
                    syllable.Clear();
                }
            }
            yield return word.Substring(i);
        }

        private int GlasLeft(string input, int i)
        {
            int count = 0;
            for (int j = i; j < input.Length; j++)
                if (Glasru.Contains(input[j]))
                    count++;
            return count;
        }

        //Кодирование Фано
        public void Fanofor3(int L, int R, string[] Res, Dictionary<string, double> dict)
        {
            int n, d; int[] a;
            if (L < R)
            {
                a = delenie_posledovatelnostyna3(L, R, dict);
                n = a[0];
                d = a[1];

                for (int i = L; i <= R; i++)
                {
                    if (i <= n)
                    {
                        Res[i] += "0";

                    }
                    else if (i > n && i <= d)
                    {
                        Res[i] += "1";
                    }
                    else
                    {
                        Res[i] += "2";
                    }
                }
                Fanofor3(L, n, Res, dict);
                Fanofor3(n + 1, d, Res, dict);
                Fanofor3(d + 1, R, Res, dict);
            }
        }

        public void Fano(int L, int R, string[] Res, Dictionary<string, double> dict)
        {
            int n;
            if (L < R)
            {
                n = Delenie_Posledovatelnosty(L, R, dict);

                for (int i = L; i <= R; i++)
                {
                    if (i <= n)
                    {
                        Res[i] += Convert.ToByte(0);
                    }
                    else
                    {
                        Res[i] += Convert.ToByte(1);
                    }
                }
                Fano(L, n, Res, dict);
                Fano(n + 1, R, Res, dict);
            }
        }

        public int[] delenie_posledovatelnostyna3(int L, int R, Dictionary<string, double> P1)
        {
            double even1 = 0;
            int m, f, p;

            for (int i = L; i <= R - 1; i++)
            {
                even1 += P1.ElementAt(i).Value;
            }

            double a = even1 / 3;
            m = R;
            double even2 = 0;

            while (even2 <= a)
            {
                m--;
                even2 += P1.ElementAt(m).Value;
                even1 -= P1.ElementAt(m).Value;
            }

            f = m;
            double even3 = 0;

            if (m > 0) m--;
            while (even3 <= a && m <= R && m >= L)
            {
                even3 += P1.ElementAt(m).Value;
                even1 -= P1.ElementAt(m).Value;
                m--;
            }

            p = m;
            int[] ss = { p, f };
            return ss;
        }

        public void Fanofor5(int L, int R, string[] Res, Dictionary<char, double> dict)
        {
            int n, d, s, q;
            int[] a;
            if (L < R)
            {
                a = delenie_posledovatelnostyna5(L, R, dict);
                n = a[0];
                d = a[1];
                s = a[2];
                q = a[3];

                for (int i = L; i <= R; i++)
                {
                    if (i <= n)
                    {
                        Res[i] += "0";

                    }
                    else if (i > n && i <= d)
                    {
                        Res[i] += "1";
                    }
                    else if (i > d && i <= s)
                    {
                        Res[i] += "2";
                    }
                    else if (i > s && i <= q)
                    {
                        Res[i] += "3";
                    }
                    else
                    {
                        Res[i] += "4";
                    }
                }
                Fanofor5(L, n, Res, dict);
                Fanofor5(n + 1, d, Res, dict);
                Fanofor5(d + 1, s, Res, dict);
                Fanofor5(s + 1, q, Res, dict);
                Fanofor5(q + 1, R, Res, dict);
            }
        }

        public void Fanofor5(int L, int R, string[] Res, Dictionary<string, double> dict)
        {
            int n, d, s, q; int[] a;
            if (L < R)
            {
                a = delenie_posledovatelnostyna5(L, R, dict);
                n = a[0];
                d = a[1];
                s = a[2];
                q = a[3];

                for (int i = L; i <= R; i++)
                {
                    if (i <= n)
                    {
                        Res[i] += "0";

                    }
                    else if (i > n && i <= d)
                    {
                        Res[i] += "1";
                    }
                    else if (i > d && i <= s)
                    {
                        Res[i] += "2";
                    }
                    else if (i > s && i <= q)
                    {
                        Res[i] += "3";
                    }
                    else
                    {
                        Res[i] += "4";
                    }
                }
                Fanofor5(L, n, Res, dict);
                Fanofor5(n + 1, d, Res, dict);
                Fanofor5(d + 1, s, Res, dict);
                Fanofor5(s + 1, q, Res, dict);
                Fanofor5(q + 1, R, Res, dict);
            }
        }

        public int[] delenie_posledovatelnostyna5(int L, int R, Dictionary<char, double> P1)
        {
            double even1 = 0;
            int[] ss;
            int m = R;
            int q, k, f, p;

            for (int i = L; i <= R - 1; i++)
            {
                even1 += P1.ElementAt(i).Value;
            }

            double a = even1 / 5;
            double even2 = 0;

            while (even2 <= a && m >= L)
            {
                m--;
                even2 += P1.ElementAt(m).Value;
                even1 -= P1.ElementAt(m).Value;
            }

            f = m;
            double even3 = 0;

            if (m > 0) m--;
            while (even3 <= a && m <= R && m >= L)
            {
                even3 += P1.ElementAt(m).Value;
                even1 -= P1.ElementAt(m).Value;
                if (m > 0) m--;
            }

            k = m;
            double even4 = 0;

            if (m > 0) m--;
            while (even4 <= a && m <= R && m >= L)
            {
                even4 += P1.ElementAt(m).Value;
                even1 -= P1.ElementAt(m).Value;
                if (m > 0) m--;
            }
            q = m;
            double even5 = 0;

            while (even5 <= a && m <= R && m >= L)
            {
                even5 += P1.ElementAt(m).Value;
                even1 -= P1.ElementAt(m).Value;
                if (m > 0) m--;
            }

            p = m;
            ss = new int[] { p, q, k, f };
            return ss;
        }

        public int[] delenie_posledovatelnostyna5(int L, int R, Dictionary<string, double> P1)
        {
            double even1 = 0;
            int[] ss;
            int m = R;
            int q, k, f, p;

            for (int i = L; i <= R - 1; i++)
            {
                even1 += P1.ElementAt(i).Value;
            }

            double a = even1 / 5;
            double even2 = 0;

            while (even2 <= a && m >= L)
            {
                m--;
                even2 += P1.ElementAt(m).Value;
                even1 -= P1.ElementAt(m).Value;
            }

            f = m;
            double even3 = 0;

            if (m > 0) m--;
            while (even3 <= a && m <= R && m >= L)
            {
                even3 += P1.ElementAt(m).Value;
                even1 -= P1.ElementAt(m).Value;
                if (m > 0) m--;
            }

            k = m;
            double even4 = 0;

            if (m > 0) m--;
            while (even4 <= a && m <= R && m >= L)
            {
                even4 += P1.ElementAt(m).Value;
                even1 -= P1.ElementAt(m).Value;
                if (m > 0) m--;
            }
            q = m;
            double even5 = 0;

            while (even5 <= a && m <= R && m >= L)
            {
                even5 += P1.ElementAt(m).Value;
                even1 -= P1.ElementAt(m).Value;
                if (m > 0) m--;
            }

            p = m;
            ss = new int[] { p, q, k, f };
            return ss;
        }

        public int Delenie_Posledovatelnosty(int L, int R, Dictionary<string, double> P1)
        {
            double even1 = 0;
            for (int i = L; i <= R - 1; i++)
            {
                even1 += P1.ElementAt(i).Value;
            }

            double even2 = P1.ElementAt(R).Value;
            int m = R;
            while (even1 >= even2)
            {
                m--;
                even1 -= P1.ElementAt(m).Value;
                even2 += P1.ElementAt(m).Value;
            }
            return m;
        }

        public void Fanofor3(int L, int R, string[] Res, Dictionary<char, double> dict)
        {
            int n, d; int[] a;
            if (L < R)
            {
                a = delenie_posledovatelnostyna3(L, R, dict);
                n = a[0];
                d = a[1];

                for (int i = L; i <= R; i++)
                {
                    if (i <= n)
                    {
                        Res[i] += "0";

                    }
                    else if (i > n && i <= d)
                    {
                        Res[i] += "1";
                    }
                    else
                    {
                        Res[i] += "2";
                    }
                }
                Fanofor3(L, n, Res, dict);
                Fanofor3(n + 1, d, Res, dict);
                Fanofor3(d + 1, R, Res, dict);
            }
        }

        public void Fano(int Left, int Right, string[] Res, Dictionary<char, double> dict)
        {
            int n;
            if (Left < Right)
            {
                n = Delenie_Posledovatelnosty(Left, Right, dict);
                for (int i = Left; i <= Right; i++)
                {
                    if (i <= n)
                    {
                        Res[i] += '0';
                    }
                    else
                    {
                        Res[i] += '1';
                    }
                }
                Fano(Left, n, Res, dict);
                Fano(n + 1, Right, Res, dict);
            }
        }

        public int[] delenie_posledovatelnostyna3(int L, int R, Dictionary<char, double> P1)
        {
            double even1 = 0;
            int m, f, p;

            for (int i = L; i <= R - 1; i++)
            {
                even1 += P1.ElementAt(i).Value;
            }

            double a = even1 / 3;
            m = R;
            double even2 = 0;

            while (even2 <= a)
            {
                m--;
                even2 += P1.ElementAt(m).Value;
                even1 -= P1.ElementAt(m).Value;
            }

            f = m;
            double even3 = 0;

            if (m > 0) m--;
            while (even3 <= a && m <= R && m >= L)
            {
                even3 += P1.ElementAt(m).Value;
                even1 -= P1.ElementAt(m).Value;
                m--;
            }

            p = m;
            int[] ss = { p, f };
            return ss;
        }

        public int Delenie_Posledovatelnosty(int L, int R, Dictionary<char, double> P1)
        {
            double even1 = 0;
            for (int i = L; i <= R - 1; i++)
            {
                even1 += P1.ElementAt(i).Value;
            }

            double even2 = P1.ElementAt(R).Value;
            int m = R;
            while (even1 >= even2)
            {
                m--;
                even1 -= P1.ElementAt(m).Value;
                even2 += P1.ElementAt(m).Value;
            }
            return m;
        }

        //Кодирование Хаффмена
        private string[] CompressBytes(Dictionary<string, int> data, string[] Res)
        {
            int c = data.Count;
            Node root = CreateHuffmanTree(data);
            Res = CreateHuffmanCode(root, c);
            return Res;
        }

        private Node CreateHuffmanTree(Dictionary<string, int> data)
        {
            PriorityQueue<Node> pq = new PriorityQueue<Node>();
            for (int j = 0; j < data.Count; j++)
                if (data.ElementAt(j).Value > 0)
                    pq.Enqueue(data.ElementAt(j).Value, new Node(data.ElementAt(j).Key, data.ElementAt(j).Value, j));
            while (pq.Size() > 1)
            {
                Node bit0 = pq.Dequeue();
                Node bit1 = pq.Dequeue();
                int freq = bit0.freq + bit1.freq;
                Node next = new Node(bit0, bit1, freq);
                pq.Enqueue(freq, next);
            }
            return pq.Dequeue();
        }
        private string[] CreateHuffmanCode(Node root, int count)
        {
            string[] Res = new string[count];
            Next(root, "");
            return Res;
            void Next(Node node, string code)
            {
                if (node.bit0 == null)
                    Res[node.number] = code;
                else
                {
                    Next(node.bit0, code + "0");
                    Next(node.bit1, code + "1");
                }
            }
        }
        private string[] CompressBytes3(Dictionary<string, int> data, string[] Res)
        {
            int c = data.Count;
            Node root = CreateHuffmanTree3(data);
            Res = CreateHuffmanCode3(root, c);
            return Res;
        }

        private Node CreateHuffmanTree3(Dictionary<string, int> data)
        {
            PriorityQueue<Node> pq = new PriorityQueue<Node>();
            for (int j = 0; j < data.Count; j++)
                if (data.ElementAt(j).Value > 0)
                    pq.Enqueue(data.ElementAt(j).Value, new Node(data.ElementAt(j).Key, data.ElementAt(j).Value, j));
            while (pq.Size() > 1)
            {
                Node bit0 = pq.Dequeue();
                Node bit1 = pq.Dequeue();
                Node bit2 = pq.Dequeue();
                int freq = (bit0 == null) ? 0 : bit0.freq;
                freq += (bit1 == null) ? 0 : bit1.freq;
                freq += (bit2 == null) ? 0 : bit2.freq;
                Node next = new Node(bit0, bit1, bit2, freq);
                pq.Enqueue(freq, next);
            }
            return pq.Dequeue();
        }
        private string[] CreateHuffmanCode3(Node root, int count)
        {
            string[] Res = new string[count];
            Next(root, "");
            return Res;
            void Next(Node node, string code)
            {
                if (node.bit0 == null && node.bit1 == null && node.bit2 == null)
                    Res[node.number] = code;
                else
                {
                    if (node.bit0 != null)
                        Next(node.bit0, code + "0");
                    if (node.bit1 != null)
                        Next(node.bit1, code + "1");
                    if (node.bit2 != null)
                        Next(node.bit2, code + "2");
                }
            }
        }
        private string[] CompressBytes5(Dictionary<string, int> data, string[] Res)
        {
            int c = data.Count;
            Node root = CreateHuffmanTree5(data);
            Res = CreateHuffmanCode5(root, c);
            return Res;
        }
        private Node CreateHuffmanTree5(Dictionary<string, int> data)
        {
            PriorityQueue<Node> pq = new PriorityQueue<Node>();
            for (int j = 0; j < data.Count; j++)
                if (data.ElementAt(j).Value > 0)
                    pq.Enqueue(data.ElementAt(j).Value, new Node(data.ElementAt(j).Key, data.ElementAt(j).Value, j));
            while (pq.Size() > 1)
            {
                Node bit0 = pq.Dequeue();
                Node bit1 = pq.Dequeue();
                Node bit2 = pq.Dequeue();
                Node bit3 = pq.Dequeue();
                Node bit4 = pq.Dequeue();
                int freq = (bit0 == null) ? 0 : bit0.freq;
                freq += (bit1 == null) ? 0 : bit1.freq;
                freq += (bit2 == null) ? 0 : bit2.freq;
                freq += (bit3 == null) ? 0 : bit3.freq;
                freq += (bit4 == null) ? 0 : bit4.freq;
                Node next = new Node(bit0, bit1, bit2, bit3, bit4, freq);
                pq.Enqueue(freq, next);
            }
            return pq.Dequeue();
        }

        private string[] CreateHuffmanCode5(Node root, int count)
        {
            string[] Res = new string[count];
            Next(root, "");
            return Res;
            void Next(Node node, string code)
            {
                if (node.bit0 == null && node.bit1 == null && node.bit2 == null && node.bit3 == null && node.bit4 == null)
                    Res[node.number] = code;
                else
                {
                    if (node.bit0 != null) Next(node.bit0, code + "0");
                    if (node.bit1 != null) Next(node.bit1, code + "1");
                    if (node.bit2 != null) Next(node.bit2, code + "2");
                    if (node.bit3 != null) Next(node.bit3, code + "3");
                    if (node.bit4 != null) Next(node.bit4, code + "4");
                }
            }
        }

        //Очистка формы
        private void ClearInfo()
        {
            resultTextBox.Text                  = "";
            countSentencesTextBox.Text          = "";
            countWordsTextBox.Text              = "";
            countSyllablesTextBox.Text          = "";
            countLetterTextBox.Text             = "";
            countSentencesUnicTextBox.Text      = "";
            countWordsUnicTextBox.Text          = "";
            countSyllablesUnicTextBox.Text      = "";
            countLetterUnicTextBox.Text         = "";
            sentencesInfoCountTextBox.Text      = "";
            wordsInfoCountTextBox.Text          = "";
            syllablesInfoCountTextBox.Text      = "";
            lettersInfoCountTextBox.Text        = "";
            SentencesElementaryTextBox.Text     = "";
            WordsElementaryTextBox.Text         = "";
            SyllablesElementaryTextBox.Text     = "";
            LettersElementaryTextBox.Text       = "";
            countSentencesTextBox.Text          = "";
            entropySentencesTextBox.Text        = "";
            countSentencesUnicTextBox.Text      = "";
            countWordsTextBox.Text              = "";
            countWordsUnicTextBox.Text          = "";
            entropyWordsTextBox.Text            = "";
            countLetterTextBox.Text             = "";
            countLetterUnicTextBox.Text         = "";
            entropyLetterTextBox.Text           = "";
            countSyllablesTextBox.Text          = "";
            countSyllablesUnicTextBox.Text      = "";
            entropySyllablesTextBox.Text        = "";
            CountSentencesHuffmanTextbox.Text   = "";
            CountWordsHuffmanTextbox.Text       = "";
            CountSyllsblesHuffmanTextbox.Text   = "";
            CountLettersHufTextbox.Text         = "";
            CountUnicSenHufTextbox.Text         = "";
            CountUnicWordsHufTextbox.Text       = "";
            CountUnicSyllablesHufTextbox.Text   = "";
            CountUnicLettersHufTextbox.Text     = "";
            entropyHuf2Sen.Text                 = "";
            entropyHuf2W.Text                   = "";
            entropyHuf2Syllables.Text           = "";
            entropyHuf2Letters.Text             = "";
            SenElementaryCountHuf2.Text         = "";
            WordsElementaryCountHuf2.Text       = "";
            SyllablElementaryCountHuf2.Text     = "";
            LettersElementaryCountHuf2.Text     = "";
            InfoCountSenHuf2.Text               = "";
            InfoCountWHuf2.Text                 = "";
            InfoCountSyllablesHuf2.Text         = "";
            InfoCountLettersHuf2.Text           = "";
            entropyFano3Sen.Text                = "";
            entropyFano3W.Text                  = "";
            entropyFano3Syllables.Text          = "";
            entropy3FanoLetters.Text            = "";
            entropyHuf3Sen.Text                 = "";
            entropyHuf3W.Text                   = "";
            entropyHuf3Syllables.Text           = "";
            entropyHuf3Letters.Text             = "";
            SenElementaryCountFano3.Text        = "";
            WordsElementaryCountFano3.Text      = "";
            SyllablElementaryCountFano3.Text    = "";
            LettersElementaryCountFano3.Text    = "";
            InfoCountSenFano3.Text              = "";
            InfoCountWFano3.Text                = "";
            InfoCountSyllablesFano3.Text        = "";
            SyllablElementaryCountFano3.Text    = "";
            SenElementaryCountHuf3.Text         = "";
            WordsElementaryCountHuf3.Text       = "";
            SyllablElementaryCountHuf3.Text     = "";
            LettersElementaryCountHuf3.Text     = "";
            InfoCountSenHuf3.Text               = "";
            InfoCountWHuf3.Text                 = "";
            InfoCountSyllablesHuf3.Text         = "";
            InfoCountLettersHuf3.Text           = "";
            entropyFano5Sen.Text                = "";
            entropyFano5W.Text                  = "";
            entropyFano5Syllables.Text          = "";
            entropy5FanoLetters.Text            = "";
            SenElementaryCountFano5.Text        = "";
            WordsElementaryCountFano5.Text      = "";
            SyllablElementaryCountFano5.Text    = "";
            LettersElementaryCountFano5.Text    = "";
            InfoCountSenFano5.Text              = "";
            InfoCountWFano5.Text                = "";
            InfoCountSyllablesFano5.Text        = "";
            InfoCountLettersFano5.Text          = "";
            entropyHuf5Sen.Text                 = "";
            entropyHuf5W.Text                   = "";
            entropyHuf5Syllables.Text           = "";
            entropyHuf5Letters.Text             = "";
            SenElementaryCountHuf5.Text         = "";
            WordsElementaryCountHuf5.Text       = "";
            SyllablElementaryCountHuf5.Text     = "";
            LettersElementaryCountHuf5.Text     = "";
            InfoCountSenHuf5.Text               = "";
            InfoCountWHuf5.Text                 = "";
            InfoCountSyllablesHuf5.Text         = "";
            InfoCountLettersHuf5.Text           = "";
            textBox50.Text                      = "";
            textBox47.Text                      = "";
            textBox54.Text                      = "";
            textBox56.Text                      = "";
            textBox49.Text                      = "";
            textBox46.Text                      = "";
            textBox52.Text                      = "";
            textBox55.Text                      = "";
            textBox48.Text                      = "";
            textBox45.Text                      = "";
            textBox51.Text                      = "";
            textBox53.Text                      = "";
            textBox114.Text                     = "";
            textBox111.Text                     = "";
            textBox118.Text                     = "";
            textBox120.Text                     = "";
            textBox113.Text                     = "";
            textBox110.Text                     = "";
            textBox116.Text                     = "";
            textBox119.Text                     = "";
            textBox66.Text                      = "";
            textBox63.Text                      = "";
            textBox70.Text                      = "";
            textBox72.Text                      = "";
            textBox65.Text                      = "";
            textBox62.Text                      = "";
            textBox68.Text                      = "";
            textBox71.Text                      = "";
            textBox64.Text                      = "";
            textBox61.Text                      = "";
            textBox67.Text                      = "";
            textBox69.Text                      = "";
            textBox18.Text                      = "";
            textBox15.Text                      = "";
            textBox58.Text                      = "";
            textBox60.Text                      = "";
            textBox17.Text                      = "";
            textBox14.Text                      = "";
            textBox20.Text                      = "";
            textBox59.Text                      = "";
            textBox16.Text                      = "";
            textBox13.Text                      = "";
            textBox19.Text                      = "";
            textBox57.Text                      = "";
            textBox6.Text                       = "";
            textBox3.Text                       = "";
            textBox10.Text                      = "";
            textBox12.Text                      = "";
            textBox5.Text                       = "";
            textBox2.Text                       = "";
            textBox8.Text                       = "";
            textBox11.Text                      = "";
            textBox4.Text                       = "";
            textBox1.Text                       = "";
            textBox7.Text                       = "";
            textBox9.Text                       = "";
        }
        //Случайно тыкнутые элементы формы
        private void AppForm_Load(object sender, EventArgs e)
        {

        }
        private void label12_Click(object sender, EventArgs e)
        {

        }
    }
}