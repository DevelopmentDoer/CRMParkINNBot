using System;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using System.Net.Http;
using System.Collections.Generic;
using Newtonsoft.Json;
using System.Configuration;

namespace TelegramBotVS2022
{
    class Program
    {
        public class Company
        {
            public msv[] items { get; set; }
        }

        public class msv
        {
            public ЮЛ юл { get; set; }
        }

        public class ЮЛ
        {
            public string НаимПолнЮЛ { get; set; }
            public Адрес адрес { get; set; }
            public ОснВидДеят оснвиддеят { get; set; }
            public msv2[] ДопВидДеят { get; set; }
        }

        public class msv2
        {
            public string Код { get; set; }
            public string Текст { get; set; }
        }

        public class ОснВидДеят
        {
            public string Код { get; set; }
            public string Текст { get; set; }
        }

        public class Адрес
        {
            public string АдресПолн { get; set; }
        }

        static Dictionary<long, int> usersChatSessions = new Dictionary<long, int>();
        static string FNSApiKey = ConfigurationSettings.AppSettings["FNSApiKey"];
        static string TelegramBotAPIKey = ConfigurationSettings.AppSettings["TGApiKey"];



        static void Main()
        {
            var client = new TelegramBotClient(TelegramBotAPIKey);
            client.StartReceiving(Update, Error);
            Console.ReadLine();
        }

        async static Task Update(ITelegramBotClient botClient, Update update, CancellationToken token)
        {
            //Возможные значения:
            //0 - Пользователь ещё не ввёл ни одну команду
            //1 - Предыдущая команда - /hello
            //2 - Предыдущая команда - /inn
            //3 - Предыдущая команда - /okved
            //4 - Предыдущая команда - /egrul
            //5 - Получаем наименования и адреса по ИНН
            //6 - Получаем коды (ОКВЭД) и виды деятельности по ИНН
            //7 - Получаем pdf-файл с выпиской из ЕГРЮЛ по ИНН

            ЮЛ юл;
            HttpClient client = new HttpClient();
            var message = update.Message;
            int doingNumber = 0;
            if (usersChatSessions.ContainsKey(message.Chat.Id) == false)
            {
                usersChatSessions.Add(message.Chat.Id, 0);
            }
            else
            {
                doingNumber = usersChatSessions[message.Chat.Id];
            }

            string[] commandList = { "1. /help - Вывести справку о доступных командах",
                                     "2. /hello - Вывести имя, фамилию, почту и ссылку на GitHub",
                                     "3. /inn - Получить наименования и адреса компаний по ИНН",
                                     "4. /okved - Вывести коды (ОКВЭД) и виды деятельности компании по ИНН",
                                     "5. /egrul - Получить pdf-файл с выпиской из ЕГРЮЛ компании по ИНН",
                                     "6. /last - Повторить последнее действие бота" };

            if (message.Text != null)
            {
                if (doingNumber >= 5)
                {
                    if (doingNumber == 5)
                    {
                        var inns = message.Text.Replace(" ", "").Split(',');
                        for (int i = 0; i < inns.Length; i++)
                            if (inns[i].Length != 10 || long.TryParse(inns[i], out long result) == false)
                            {
                                await botClient.SendTextMessageAsync(message.Chat.Id, "<b>Вы ввели некорректные ИНН, попробуйте ещё раз</b>\n<i>Пример: 8390278912, 4902762198, 4092894766</i>", parseMode: ParseMode.Html);
                                return;
                            }

                        usersChatSessions[message.Chat.Id] = 2;
                        await botClient.SendTextMessageAsync(message.Chat.Id, "<b>Наименования и адреса компаний по ИНН:</b>\n", parseMode: ParseMode.Html);
                        for (int i = 0; i < inns.Length; i++)
                        {
                            var responseString = await client.GetStringAsync("https://api-fns.ru/api/egr?req=" + inns[i] + "&key=" + FNSApiKey);

                            if (responseString == "" || responseString == null || JsonConvert.DeserializeObject<Company>(responseString).items == null || JsonConvert.DeserializeObject<Company>(responseString).items.Length == 0)
                            {
                                await botClient.SendTextMessageAsync(message.Chat.Id, (i + 1).ToString() + ". <i>ИНН:</i> " + inns[i] + " Компании не существует, либо возникли проблемы с соединением с ФНС", parseMode: ParseMode.Html);
                                continue;
                            }

                            юл = JsonConvert.DeserializeObject<Company>(responseString).items[0].юл;

                            await botClient.SendTextMessageAsync(message.Chat.Id, (i + 1).ToString() + ". <i>ИНН:</i> " + inns[i] + " <i>Наименование:</i> " + юл.НаимПолнЮЛ + " <i>Юридический адрес:</i> " + юл.адрес.АдресПолн, parseMode: ParseMode.Html);
                        }
                    }
                    else if (doingNumber == 6)
                    {
                        var inn = message.Text;
                        if (inn.Length != 10 || long.TryParse(inn, out long result) == false)
                        {
                            await botClient.SendTextMessageAsync(message.Chat.Id, "<b>Вы ввели некорректные ИНН, попробуйте ещё раз</b>\n<i>Пример: 8390278912 или 4092894766</i>", parseMode: ParseMode.Html);
                            return;
                        }

                        usersChatSessions[message.Chat.Id] = 3;

                        var responseString = await client.GetStringAsync("https://api-fns.ru/api/egr?req=" + inn + "&key=" + FNSApiKey);

                        if (responseString == "" || responseString == null || JsonConvert.DeserializeObject<Company>(responseString).items == null || JsonConvert.DeserializeObject<Company>(responseString).items.Length == 0)
                        {
                            await botClient.SendTextMessageAsync(message.Chat.Id, "Компании не существует, либо возникли проблемы с соединением с ФНС", parseMode: ParseMode.Html);
                            return;
                        }

                        юл = JsonConvert.DeserializeObject<Company>(responseString).items[0].юл;

                        await botClient.SendTextMessageAsync(message.Chat.Id, "<b>Основной вид деятельности компании:</b>\n<i>Код: </i>" + юл.оснвиддеят.Код + " <i>Вид деятельности: </i>" + юл.оснвиддеят.Текст, parseMode: ParseMode.Html);

                        if (юл.ДопВидДеят.Length == 0)
                        {
                            await botClient.SendTextMessageAsync(message.Chat.Id, "<i>Дополнительные виды деятельности компании отсутствуют</i>", parseMode: ParseMode.Html);
                            return;
                        }

                        string resultCompany1 = юл.ДопВидДеят[0].Код;
                        string resultCompany2 = юл.ДопВидДеят[0].Текст;
                        for (int i = 1; i <= юл.ДопВидДеят.Length - 1; i++)
                        {
                            resultCompany1 += "=" + юл.ДопВидДеят[i].Код;
                            resultCompany2 += "=" + юл.ДопВидДеят[i].Текст;
                        }

                        var deat1 = resultCompany1.Split('=');
                        var deat2 = resultCompany2.Split('=');
                        var deat3 = resultCompany2.Split('=');
                        Array.Sort(deat3);

                        for (int i = 0; i < deat1.Length; i++)
                            await botClient.SendTextMessageAsync(message.Chat.Id, "<b>Дополнительный вид деятельности компании:</b>\n<i>Код: </i>" + deat1[Array.IndexOf(deat2, deat3[i])] + " <i>Вид деятельности: </i>" + deat3[i], parseMode: ParseMode.Html);
                    }
                    else if (doingNumber == 7)
                    {
                        var inn = message.Text;

                        if (inn.Length != 10 || long.TryParse(inn, out long result) == false)
                        {
                            await botClient.SendTextMessageAsync(message.Chat.Id, "<b>Вы ввели некорректные ИНН, попробуйте ещё раз</b>\n<i>Пример: 8390278912 или 4092894766</i>", parseMode: ParseMode.Html);
                            return;
                        }

                        usersChatSessions[message.Chat.Id] = 4;
                        await botClient.SendDocumentAsync(message.Chat.Id, document: InputFile.FromStream(await client.GetStreamAsync("https://api-fns.ru/api/vyp?req=" + inn + "&key=" + FNSApiKey), "EGRUL" + inn + ".pdf"), caption: "<b>Ваш pdf-file с выпиской из ЕГРЮЛ компании</b>", parseMode: ParseMode.Html);
                    }

                    return;
                }

                if (message.Text.ToLower().Contains("/last") && doingNumber == 0)
                {
                    await botClient.SendTextMessageAsync(message.Chat.Id, "<b>Вы ещё не ввели ни одной команды</b>", parseMode: ParseMode.Html);
                    return;
                }
                if (message.Text.ToLower().Contains("/last") == false)
                    doingNumber = 0;

                if (message.Text.ToLower().Contains("/start"))
                    await botClient.SendTextMessageAsync(message.Chat.Id, "Добро пожаловать в <b>CRMpark Bot!</b> Используйте меню слева от окна диалога или /help, чтобы получить список команд", parseMode: ParseMode.Html);
                else if (message.Text.ToLower().Contains("/help"))
                    await botClient.SendTextMessageAsync(message.Chat.Id, "<b>Список команд:</b>\n" + String.Join("\n", commandList), parseMode: ParseMode.Html);
                else if (doingNumber == 1 || message.Text.ToLower().Contains("/hello"))
                {
                    await botClient.SendTextMessageAsync(message.Chat.Id, "<b>Данные о разработчике:</b>\n1. ФИО - Александр Кукушкин\n2. Почта - expectprotectcom@gmail.com\n3. Ссылка на GitHub - ", parseMode: ParseMode.Html);
                    usersChatSessions[message.Chat.Id] = 1;
                }
                else if (doingNumber == 2 || message.Text.ToLower().Contains("/inn"))
                {
                    await botClient.SendTextMessageAsync(message.Chat.Id, "<b>Введите ИНН через запятую</b>\n<i>Пример: 8390278912, 4902762198, 4092894766</i>", parseMode: ParseMode.Html);
                    usersChatSessions[message.Chat.Id] = 5;
                }
                else if (doingNumber == 3 || message.Text.ToLower().Contains("/okved"))
                {
                    await botClient.SendTextMessageAsync(message.Chat.Id, "<b>Введите ИНН компании</b>\n<i>Пример: 8390278912 или 4092894766</i>", parseMode: ParseMode.Html);
                    usersChatSessions[message.Chat.Id] = 6;
                }
                else if (doingNumber == 4 || message.Text.ToLower().Contains("/egrul"))
                {
                    await botClient.SendTextMessageAsync(message.Chat.Id, "<b>Введите ИНН компании</b>\n<i>Пример: 8390278912 или 4092894766</i>", parseMode: ParseMode.Html);
                    usersChatSessions[message.Chat.Id] = 7;
                }
                else
                    await botClient.SendTextMessageAsync(message.Chat.Id, "<b>Некорректная команда.</b> Используйте меню слева от окна диалога или /help, чтобы получить список команд", parseMode: ParseMode.Html);
                return;
            }
        }

        private static Task Error(ITelegramBotClient arg1, Exception arg2, CancellationToken arg3)
        {
            return null;
        }
    }
}
