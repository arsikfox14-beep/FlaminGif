using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;

namespace Video2Gif
{
    /// <summary>
    /// Простая локализация: словарь русских строк и авто-перевод текста
    /// элементов управления. Язык хранится в settings.txt и применяется
    /// после перезапуска.
    /// </summary>
    internal static class Loc
    {
        public const string Russian = "ru";
        public const string EnglishCode = "en";

        private static readonly Dictionary<string, string> Map =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                { "   ·   доступно: ", "   ·   available: " },
                { "   ·   пробел — заново", "   ·   space — restart" },
                { "   ·   рекорд ", "   ·   best " },
                { " — видео и картинки в GIF", " — video and images to GIF" },
                { " (у вас ", " (you have " },
                { " · локально", " · local" },
                { ".\nСкачать и установить сейчас?", ".\nDownload and install now?" },
                { "0 — весь клип", "0 — full clip" },
                { "0 — оригинальная ширина", "0 — original width" },
                { "Discord — чат сообщества", "Discord — community chat" },
                { "FFmpeg ещё готовится, подождите пару секунд.", "FFmpeg is still preparing, wait a couple of seconds." },
                { "GIF будет повторяться без остановки", "GIF will loop forever" },
                { "GIF-инструменты · кадры и сборка", "GIF tools · frames and assembly" },
                { "GTA 6 — 18 ноя 15:00 – 20 ноя 00:00.", "GTA 6 — Nov 18, 15:00 – Nov 20, 00:00." },
                { "Sierra — плавнее", "Sierra — smoother" },
                { "YouTube и другие сайты", "YouTube and other sites" },
                { "Авто по дате: Сакура 1 мар–1 июн, Лето 1 июн–1 сен, Хэллоуин 1 окт–1 ноя,\nСнежная 1 дек–1 янв, GTA 6 — 18 ноя 15:00 – 20 ноя 00:00.", "Auto by date: Sakura Mar 1–Jun 1, Summer Jun 1–Sep 1, Halloween Oct 1–Nov 1,\nSnowy Dec 1–Jan 1, GTA 6 — Nov 18, 15:00 – Nov 20, 00:00." },
                { "Авто", "Auto" },
                { "Авторы", "Credits" },
                { "Байер — компактнее", "Bayer — smaller" },
                { "без лимита", "no limit" },
                { "Без сглаживания", "No dithering" },
                { "Бесконечный цикл", "Loop forever" },
                { "Быстрый режим", "Fast mode" },
                { "В буфере нет картинки, файла или ссылки.", "Clipboard has no image, file or link." },
                { "Версия ", "Version " },
                { "весь клип", "full clip" },
                { "видео и картинки → GIF", "video and images → GIF" },
                { "Видео: MP4, MKV, MOV, WEBM   ·   Картинки: PNG, JPG, WEBP, BMP, TIFF", "Video: MP4, MKV, MOV, WEBM   ·   Images: PNG, JPG, WEBP, BMP, TIFF" },
                { "Видеоредактор · обрезка видео и фото", "Video editor · trim video and photos" },
                { "Вставить из буфера", "Paste" },
                { "Выберите видео или картинку", "Choose a video or image" },
                { "Выбрать файл…", "Choose file…" },
                { "Готовимся…", "Preparing…" },
                { "Готово. Программа перезапустится с новой версией.", "Done. The app will restart with the new version." },
                { "Готово", "Done" },
                { "Длительность — число секунд от 0. 0 или пусто — весь клип.", "Duration in seconds from 0. 0 or empty — full clip." },
                { "Длительность, с", "Duration, s" },
                { "Для картинки время и кадры\nне нужны: получится GIF\nиз одного кадра.", "For an image, time and frames\nare not needed: you get a GIF\nfrom a single frame." },
                { "Доступна версия ", "Version available " },
                { "Доступна новая версия ", "New version available " },
                { "Другой файл", "Another file" },
                { "Здесь появится GIF", "GIF will appear here" },
                { "Змейка — ", "Snake — " },
                { "Змейка", "Snake" },
                { "Игра окончена", "Game over" },
                { "Играть · Змейка", "Play · Snake" },
                { "или нажмите, чтобы выбрать", "or click to choose" },
                { "Кадров в секунду — от 1 до 60.", "Frames per second — 1 to 60." },
                { "Кадры/с", "FPS" },
                { "картинка · ", "image · " },
                { "КАРТИНКА · ", "IMAGE · " },
                { "Конвертация отменена.", "Conversion cancelled." },
                { "Копировать", "Copy" },
                { "Лимит размера — от 0 до 1000 МБ. Пусто или 0 — без лимита.", "Size limit from 0 to 1000 MB. Empty or 0 — no limit." },
                { "Лимит, МБ", "Limit, MB" },
                { "Музыка из Spotify", "Music from Spotify" },
                { "Нажмите «Скачать и установить».", "Click “Download and install”." },
                { "Настройки — ", "Settings — " },
                { "Настройки · тема оформления", "Settings · theme" },
                { "Настройки", "Settings" },
                { "Настройте параметры и нажмите «Сделать GIF»", "Adjust the settings and press “Make GIF”" },
                { "Начало должно быть числом от 0 секунд.", "Start must be a number of seconds from 0." },
                { "Начало совпадает с концом видео.", "Start matches the end of the video." },
                { "Начало, с", "Start, s" },
                { "не используется", "not used" },
                { "Не удалось вставить из буфера.\n\n", "Could not paste from clipboard.\n\n" },
                { "Не удалось запустить приложение.\n\n", "Could not start the app.\n\n" },
                { "Не удалось запустить установку: ", "Could not start installation: " },
                { "Не удалось открыть готовый GIF: ", "Could not open the GIF: " },
                { "Не удалось открыть ссылку: ", "Could not open the link: " },
                { "Не удалось открыть файл.\n\n", "Could not open the file.\n\n" },
                { "Не удалось подготовить FFmpeg.\n\n", "Could not prepare FFmpeg.\n\n" },
                { "Не удалось проверить обновления.", "Could not check for updates." },
                { "Не удалось прочитать файл", "Could not read the file" },
                { "Не удалось прочитать файл.", "Could not read the file." },
                { "Не удалось скачать обновление.", "Could not download the update." },
                { "Не удалось скопировать GIF.\n\n", "Could not copy the GIF.\n\n" },
                { "Не удалось создать временную папку.\n\n", "Could not create a temp folder.\n\n" },
                { "Не удалось сохранить файл.\n\n", "Could not save the file.\n\n" },
                { "неизвестная ошибка", "unknown error" },
                { "Обновление — ", "Update — " },
                { "Обновление программы", "App update" },
                { "Обновления", "Updates" },
                { "Один проход вместо двух: быстрее, но палитра чуть беднее", "One pass instead of two: faster, but the palette is a bit poorer" },
                { "оригинал", "original" },
                { "Останавливаем…", "Stopping…" },
                { "От 1 до 60 кадров в секунду", "1 to 60 frames per second" },
                { "Открыть", "Open" },
                { "Отменить", "Cancel" },
                { "Пауза", "Pause" },
                { "Перетащите файл сюда", "Drag a file here" },
                { "Перезапустить сейчас?", "Restart now?" },
                { "Подготовка…", "Preparing…" },
                { "Поддержать проект · Donate", "Support the project · Donate" },
                { "Позже", "Later" },
                { "Пробел — продолжить", "Space — resume" },
                { "Проверить обновления", "Check for updates" },
                { "Проверяем…", "Checking…" },
                { "Результат", "Result" },
                { "Светлая", "Light" },
                { "Сглаживание переходов", "Dithering" },
                { "Сделать GIF", "Make GIF" },
                { "Сжимаем под лимит…", "Compressing to fit the limit…" },
                { "Скачать видео, музыку или TikTok", "Download video, music or TikTok" },
                { "Скачать и установить сейчас?", "Download and install now?" },
                { "Скачать и установить", "Download and install" },
                { "Скачиваем обновление…", "Downloading update…" },
                { "Скопировано", "Copied" },
                { "Слишком короткий фрагмент — выберите другую длительность.", "Clip is too short — choose another duration." },
                { "Сначала выберите видео или картинку.", "Choose a video or image first." },
                { "Снежная тема", "Snowy" },
                { "Снежная", "Snowy" },
                { "Сохранено", "Saved" },
                { "Сохранить GIF", "Save GIF" },
                { "Сохранить", "Save" },
                { "Стрелка или WASD — начать", "Arrow or WASD — start" },
                { "Стрелки или WASD · пробел — пауза · Esc — выход", "Arrows or WASD · space — pause · Esc — exit" },
                { "Счёт ", "Score " },
                { "Счёт: ", "Score: " },
                { "Тема оформления", "Theme" },
                { "Тема применяется сразу и запоминается.", "Theme applies instantly and is remembered." },
                { "Тёмная", "Dark" },
                { "Установлена последняя версия.", "You have the latest version." },
                { "Установлено: ", "Installed: " },
                { "Файл не найден.", "File not found." },
                { "Хэллоуин", "Halloween" },
                { "Сакура", "Sakura" },
                { "Лето", "Summer" },
                { "Читаем файл…  ", "Reading file…  " },
                { "Ширина должна быть от 0 до 1920 пикселей.", "Width must be 0 to 1920 pixels." },
                { "Ширина, px", "Width, px" },
                { "Язык интерфейса", "Interface language" },
                { "Русский", "Russian" },
                { "Английский", "English" },
                { "Скачать видео или музыку", "Download video or music" },
                { "Скачать музыку из Spotify", "Download music from Spotify" },
                { "YouTube, TikTok, SoundCloud, VK и другие сайты · нужен интернет", "YouTube, TikTok, SoundCloud, VK and other sites · internet required" },
                { "Треки, музыка и подкасты · ссылки из YouTube и Spotify в MP3 или WAV", "Tracks, music and podcasts · YouTube and Spotify links to MP3 or WAV" },
                { "Формат", "Format" },
                { "Видео MP4", "Video MP4" },
                { "Музыка MP3", "Music MP3" },
                { "WAV · 1411 кбит/с", "WAV · 1411 kbps" },
                { "MP3 · 320 кбит/с", "MP3 · 320 kbps" },
                { "Качество видео", "Video quality" },
                { "Лучшее", "Best" },
                { "Куда сохранить", "Save to" },
                { "Выбрать", "Choose" },
                { "Скачать", "Download" },
                { "Закрыть", "Close" },
                { "Отмена", "Cancel" },
                { "Вставить", "Paste" },
                { "Скачать из TikTok", "Download from TikTok" },
                { "Без водяного знака · интернет нужен только для загрузки", "No watermark · internet is only needed to download" },
                { "Вставьте ссылку на видео или фото-пост", "Paste a video or photo post link" },
                { "GIF-инструменты", "GIF tools" },
                { "GIF-файл", "GIF file" },
                { "Трек, альбом или плейлист · находим на YouTube и сохраняем в MP3 или WAV", "Track, album or playlist · find it on YouTube and save as MP3 or WAV" },
                { "Разбить GIF на кадры или собрать его обратно", "Split a GIF into frames or assemble it back" },
                { "Разбить GIF на кадры", "Split GIF into frames" },
                { "Выбрать GIF", "Choose GIF" },
                { "Разбить на кадры", "Split into frames" },
                { "Открыть папку", "Open folder" },
                { "Собрать GIF из кадров", "Build GIF from frames" },
                { "Папка с картинками", "Folder with images" },
                { "Выбрать папку", "Choose folder" },
                { "Ширина, px (0=ориг.)", "Width, px (0=orig.)" },
                { "Собрать GIF", "Build GIF" },
                { "PNG/JPG/BMP, по порядку имён", "PNG/JPG/BMP, sorted by name" },
                { "Открыть GIF", "Open GIF" },
                { "Видеоредактор", "Video editor" },
                { "Обрезка видео и фото · самый минимум", "Trim video and photos · the bare minimum" },
                { "Убрать фон", "Remove background" },
                { "Убрать фон · фото и GIF", "Remove background · photos and GIFs" },
                { "Фото и GIF · удаление фона по цвету", "Photos and GIFs · remove background by color" },
                { "Цвет фона", "Background color" },
                { "Взять из угла", "Take from corner" },
                { "Пипетка", "Eyedropper" },
                { "Кликните по фону на картинке", "Click the background in the image" },
                { "Пипетка выключена", "Eyedropper off" },
                { "Только связанный фон", "Connected background only" },
                { "Не трогать похожие цвета внутри объекта", "Keep similar colors inside the subject" },
                { "Сохранить PNG", "Save PNG" },
                { "Сохранить GIF без фона", "Save GIF without background" },
                { "Сохранить фото без фона", "Save photo without background" },
                { "Откройте фото или GIF", "Open a photo or GIF" },
                { "Выберите фото или GIF", "Choose a photo or GIF" },
                { "Не удалось убрать фон.", "Could not remove the background." },
                { "Подождите, идёт обработка…", "Wait, processing…" },
                { "Открываем…", "Opening…" },
                { "Сохраняем…", "Saving…" },
                { "Кадр ", "Frame " },
                { " из ", " of " },
                { "Открыть файл", "Open file" },
                { "Потяните бегунки, затем «Проверить» — фрагмент проиграется со звуком.", "Drag the handles, then “Preview” — the clip plays with sound." },
                { "В этом файле нет звуковой дорожки — видео сохранится без звука.", "This file has no audio track — the video will be saved without sound." },
                { "Начало: 0,0 с", "Start: 0.0 s" },
                { "Выбрано: 0,0 с", "Selected: 0.0 s" },
                { "Конец: 0,0 с", "End: 0.0 s" },
                { "▶ Проверить", "▶ Preview" },
                { "Весь кадр", "Full frame" },
                { "Текст и поверх", "Text and overlay" },
                { "Подпись и вторая картинка на фото или видео", "Caption and a second image on photo or video" },
                { "Подпись", "Caption" },
                { "необязательно", "optional" },
                { "Сверху", "Top" },
                { "Снизу", "Bottom" },
                { "Картинка поверх", "Overlay image" },
                { "PNG или JPG", "PNG or JPG" },
                { "Угол", "Corner" },
                { "Размер, %", "Size, %" },
                { "Кадров/с", "FPS" },
                { "Откройте видео или фото", "Open a video or photo" },
                { "Открыть видео или фото", "Open a video or photo" },
                { "Сохранить видео", "Save video" },
                { "Скачиваем…", "Downloading…" },
                { "Отменено.", "Cancelled." },
                { "Готово.", "Done." },
                { "Ошибка", "Error" },
                { "Проверьте ссылку.", "Check the link." },
                { "Вставьте ссылку.", "Paste a link." },
                { "Свои темы", "Custom themes" },
                { "— свои темы —", "— custom themes —" },
                { "— нет своих тем —", "— no custom themes —" },
                { "Создать…", "Create…" },
                { "Удалить", "Delete" },
                { "Название", "Name" },
                { "Основа", "Base" },
                { "Цвет акцента", "Accent color" },
                { "Введите название темы.", "Enter a theme name." },
                { "Фон", "Background" },
                { "Настроить фон…", "Set background…" },
                { "Фон…", "Background…" },
                { "Фон: не задан", "Background: not set" },
                { "не задан", "not set" },
                { "Фон приложения", "App background" },
                { "Цвет…", "Color…" },
                { "Картинка или GIF…", "Image or GIF…" },
                { "Сброс", "Reset" },
                { "Сбросить", "Reset" },
                { "Фон виден за карточками. GIF анимируется.", "The background shows behind the cards. GIFs animate." },
                { "Сейчас: стандартный фон темы", "Now: theme background" },
                { "Выберите картинку или GIF", "Choose an image or GIF" },
                { "Получаем данные из Spotify…", "Getting data from Spotify…" },
                { "Spotify ограничил доступ, пробуем другой способ…", "Spotify is restricted, trying another way…" }
            };

        private static readonly KeyValuePair<string, string>[] Prefixes =
        {
            new KeyValuePair<string, string>("Настройки — ", "Settings — "),
            new KeyValuePair<string, string>("Обновление — ", "Update — "),
            new KeyValuePair<string, string>("Змейка — ", "Snake — "),
            new KeyValuePair<string, string>("Версия ", "Version "),
            new KeyValuePair<string, string>("Счёт ", "Score "),
            new KeyValuePair<string, string>("Счёт: ", "Score: "),
            new KeyValuePair<string, string>("Доступна версия ", "Version available "),
            new KeyValuePair<string, string>("Доступна новая версия ", "New version available "),
            new KeyValuePair<string, string>("Установлено: ", "Installed: "),
            new KeyValuePair<string, string>(" (у вас ", " (you have "),
            new KeyValuePair<string, string>("Начало: ", "Start: "),
            new KeyValuePair<string, string>("Конец: ", "End: "),
            new KeyValuePair<string, string>("Выбрано: ", "Selected: "),
            new KeyValuePair<string, string>("Скачано: ", "Downloaded: "),
            new KeyValuePair<string, string>("Сохранено: ", "Saved: "),
            new KeyValuePair<string, string>("Файл: ", "File: "),
            new KeyValuePair<string, string>("Готово: ", "Done: "),
            new KeyValuePair<string, string>("Ошибка: ", "Error: "),
            new KeyValuePair<string, string>("Скачано файлов: ", "Files downloaded: "),
            new KeyValuePair<string, string>("Своя тема — ", "Custom theme — "),
            new KeyValuePair<string, string>("Фон — ", "Background — "),
            new KeyValuePair<string, string>("Сейчас: цвет ", "Now: color "),
            new KeyValuePair<string, string>("Сейчас: ", "Now: "),
            new KeyValuePair<string, string>("Фон: ", "Background: "),
            new KeyValuePair<string, string>("Прозрачность карточек: ", "Card transparency: "),
            new KeyValuePair<string, string>("Прозрачность фона: ", "Background transparency: "),
            new KeyValuePair<string, string>("Удалить тему «", "Delete theme “"),
            new KeyValuePair<string, string>("Видеоредактор — ", "Video editor — "),
            new KeyValuePair<string, string>("Убрать фон — ", "Remove background — "),
            new KeyValuePair<string, string>("Цвет фона: ", "Background color: "),
            new KeyValuePair<string, string>("Допуск: ", "Tolerance: "),
            new KeyValuePair<string, string>("Края: ", "Edges: "),
            new KeyValuePair<string, string>("GIF-инструменты — ", "GIF tools — "),
            new KeyValuePair<string, string>("Текст и поверх — ", "Text and overlay — "),
            new KeyValuePair<string, string>("Скачать из TikTok — ", "Download from TikTok — "),
            new KeyValuePair<string, string>(" — видео и картинки в GIF", " — video and images to GIF")
        };

        private static readonly KeyValuePair<string, string>[] Suffixes =
        {
            new KeyValuePair<string, string>(" — видео и картинки в GIF", " — video and images to GIF"),
            new KeyValuePair<string, string>(" · локально", " · local"),
            new KeyValuePair<string, string>(" · фото", " · photo"),
            new KeyValuePair<string, string>("   ·   доступно: ", "   ·   available: "),
            new KeyValuePair<string, string>(" файл(ов).", " file(s)."),
            new KeyValuePair<string, string>("»?", "”?")
        };
        public static string Language { get; private set; }

        static Loc()
        {
            Language = Russian;
        }

        public static bool IsEnglish
        {
            get { return Language == EnglishCode; }
        }

        public static void Load()
        {
            string code = AppSettings.Get("language", Russian).Trim().ToLowerInvariant();
            Language = code == EnglishCode ? EnglishCode : Russian;
        }

        public static void SaveLanguage(string code)
        {
            Language = code == EnglishCode ? EnglishCode : Russian;
            AppSettings.Set("language", Language);
        }

        public static string T(string text)
        {
            if (!IsEnglish || string.IsNullOrEmpty(text)) return text;
            string translated;
            if (Map.TryGetValue(text, out translated)) return translated;
            for (int i = 0; i < Prefixes.Length; i++)
            {
                if (text.StartsWith(Prefixes[i].Key, StringComparison.Ordinal))
                {
                    return Prefixes[i].Value + text.Substring(Prefixes[i].Key.Length);
                }
            }
            for (int i = 0; i < Suffixes.Length; i++)
            {
                if (text.EndsWith(Suffixes[i].Key, StringComparison.Ordinal))
                {
                    return text.Substring(0, text.Length - Suffixes[i].Key.Length) + Suffixes[i].Value;
                }
            }
            return text;
        }

        private static Timer watcher;

        public static void InstallWatcher()
        {
            if (watcher != null) return;
            watcher = new Timer();
            watcher.Interval = 400;
            watcher.Tick += delegate
            {
                if (!IsEnglish) return;
                TranslateAll();
            };
            watcher.Start();
            TranslateAll();
        }

        public static void TranslateAll()
        {
            foreach (Form form in Application.OpenForms)
            {
                if (form != null && form.IsHandleCreated)
                {
                    TranslateTree(form);
                }
            }
        }

        private static void TranslateTree(Control root)
        {
            try
            {
                if (!(root is TextBoxBase) && !(root is NumericUpDown))
                {
                    string text = root.Text;
                    string translated = T(text);
                    if (!string.Equals(text, translated, StringComparison.Ordinal))
                    {
                        root.Text = translated;
                    }
                }

                ComboBox combo = root as ComboBox;
                if (combo != null)
                {
                    for (int i = 0; i < combo.Items.Count; i++)
                    {
                        string item = combo.Items[i] as string;
                        if (item == null) continue;
                        string translated = T(item);
                        if (!string.Equals(item, translated, StringComparison.Ordinal))
                        {
                            combo.Items[i] = translated;
                        }
                    }
                }

                ToolStrip strip = root as ToolStrip;
                if (strip != null) TranslateStrip(strip);
                if (root.ContextMenuStrip != null) TranslateStrip(root.ContextMenuStrip);

                foreach (Control child in root.Controls)
                {
                    TranslateTree(child);
                }
            }
            catch
            {
            }
        }

        private static void TranslateStrip(ToolStrip strip)
        {
            foreach (ToolStripItem item in strip.Items)
            {
                string text = item.Text;
                string translated = T(text);
                if (!string.Equals(text, translated, StringComparison.Ordinal))
                {
                    item.Text = translated;
                }
                ToolStripMenuItem menu = item as ToolStripMenuItem;
                if (menu != null) TranslateStrip(menu.DropDown);
            }
        }
    }
}
