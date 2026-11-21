# FlaminGif для Android

Отдельный проект: тот же FlaminGif, но для телефонов на Android.
Внутри — та же мобильная страница (видео и картинки → GIF, темы, скачивание
TikTok и прямых ссылок), упакованная в APK с доступом к галерее и сети.

by DeepSeek v4.1 flash & arsikfox14 & dismasdismavich

## Что умеет

- Видео → GIF и картинка → GIF, всё считается на телефоне.
- Начало и длительность, ширина, 1–60 кадров/с, Sierra / Байер / без сглаживания.
- Режим GIF: «Быстро» (может пропускать кадры) или «Качественно» (все кадры).
- Бесконечный цикл или один проигрыш, прогресс и отмена.
- Сохранение GIF, фото и видео в галерею (Pictures/FlaminGif, Movies/FlaminGif),
  системное «Поделиться» и всплывающее подтверждение «Сохранено в галерею».
- Темы как в Windows-версии: авто по дате, светлая, тёмная, снежная,
  хэллоуин, GTA 6 и Gamesense. Цвет системных панелей подстраивается.
- Одна кнопка «Скачать»: ссылка TikTok, видео или картинка определяются
  автоматически. Скачивание идёт через нативный HTTP, без ограничений CORS.
- Ссылки Donate и Discord, как в Windows-версии.
- Мини-игра «Змейка»: кнопка-геймпад в шапке, свайпы и D-pad, рекорд запоминается.
- Выбор файлов через системный проводник (видео и картинки).

## Чего нет

- YouTube и Spotify остаются только в Windows-версии `FlaminGif.exe`.
- TikTok-API — сторонние сервисы: если они недоступны, видео откроется
  в браузере для сохранения вручную.
- Нет встроенного FFmpeg: форматы ограничены тем, что умеет WebView
  (обычно MP4/H.264, WebM, MOV).

## Сборка

Нужны JDK 17+ (проверено на JDK 21) и PowerShell 5+.
Android Studio и Gradle не требуются.

```powershell
# один раз: скачать минимальный SDK (platform 34 + build-tools 34 + R8)
powershell -ExecutionPolicy Bypass -File setup-sdk.ps1

# собрать APK
powershell -ExecutionPolicy Bypass -File build.ps1

# собрать и сразу положить на рабочий стол
powershell -ExecutionPolicy Bypass -File build.ps1 -Desktop
```

Результат: `FlaminGif.apk` в корне проекта (около 85 КБ — внутри нет
FFmpeg, только WebView-страница и небольшой Java-слой).

Сборка без Gradle: `aapt2 compile/link` → `javac` → `R8/D8` → `zip` →
`zipalign` → `apksigner`. D8 из build-tools 34 оказался с багом на анонимных
классах, поэтому используется отдельный `r8.jar` с Google Maven
(скачивается автоматически). Подпись — самодельный ключ
`keystore/video2gif.jks` (пароль `video2gif`), он создаётся при первой сборке.
Пакет оставлен `com.video2gif.app`, чтобы новый APK ставился обновлением
поверх старого; на экране приложение называется FlaminGif.
Храните его: с другим ключом APK не установится поверх старого, только
через удаление.

## Структура

```
AndroidManifest.xml        манифест, разрешения, версия
src/…/MainActivity.java    WebView, выбор файлов, сеть, сохранение
assets/index.html          интерфейс (копия src web/index.html)
assets/android-bridge.js   мост JS ↔ Java
res/                       строки, цвета, тема, иконки
tools/make-icons.ps1       перегенерация иконок
setup-sdk.ps1              загрузка Android SDK
build.ps1                  сборка APK
```

## Как это работает

`index.html` — обычная страница. `android-bridge.js` подменяет `fetch`
на нативный HTTP (поэтому TikTok-API и прокси работают без CORS), перехватывает
скачивание ссылок и `blob:`-файлов и отдаёт их в Java. Java складывает файлы
в галерею через MediaStore (Images/Video, Android 10+) или в
Pictures/FlaminGif и Movies/FlaminGif с MediaScanner (Android 7–9),
показывает тосты и перекрашивает статус-бар под выбранную тему.

Минимальная версия — Android 7.0 (API 24), целевая — Android 14 (API 34).
Страница использует современный JavaScript, поэтому Android System WebView
лучше держать обновлённым (на практике он обновляется через Google Play).
