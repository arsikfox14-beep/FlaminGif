package com.arsikfox14.video2gif;

import android.Manifest;
import android.annotation.SuppressLint;
import android.app.Activity;
import android.content.ActivityNotFoundException;
import android.content.ContentValues;
import android.content.Intent;
import android.content.pm.PackageManager;
import android.graphics.Color;
import android.media.MediaScannerConnection;
import android.net.Uri;
import android.os.Build;
import android.os.Bundle;
import android.os.Environment;
import android.provider.MediaStore;
import android.util.Base64;
import android.util.Log;
import android.view.View;
import android.view.WindowInsetsController;
import android.webkit.JavascriptInterface;
import android.webkit.ValueCallback;
import android.webkit.WebChromeClient;
import android.webkit.WebResourceRequest;
import android.webkit.WebSettings;
import android.webkit.WebView;
import android.webkit.WebViewClient;
import android.widget.Toast;

import org.json.JSONObject;

import java.io.ByteArrayOutputStream;
import java.io.File;
import java.io.FileOutputStream;
import java.io.IOException;
import java.io.InputStream;
import java.io.OutputStream;
import java.net.HttpURLConnection;
import java.net.URL;
import java.nio.charset.StandardCharsets;
import java.util.HashMap;
import java.util.Locale;
import java.util.Map;

/**
 * video2gif для Android: вся логика — в assets/index.html,
 * Java отвечает только за выбор файлов, сеть и сохранение в галерею.
 */
public class MainActivity extends Activity {

    private static final String TAG = "video2gif";
    private static final int REQ_FILE = 1001;
    private static final int REQ_STORAGE = 1002;
    private static final long MAX_TEXT = 6L * 1024 * 1024;
    private static final String UA =
            "Mozilla/5.0 (Linux; Android 13) AppleWebKit/537.36 " +
            "(KHTML, like Gecko) Chrome/124.0 Mobile Safari/537.36";

    private WebView web;
    private ValueCallback<Uri[]> fileCallback;
    private final Object chunkLock = new Object();
    private final Map<String, OutputStream> chunkStreams = new HashMap<>();
    private final Map<String, Uri> chunkUris = new HashMap<>();
    private final Map<String, File> chunkFiles = new HashMap<>();
    private final Map<String, String> chunkNames = new HashMap<>();
    private final Map<String, String> chunkMimes = new HashMap<>();

    @SuppressLint({"SetJavaScriptEnabled", "AddJavascriptInterface"})
    @Override
    protected void onCreate(Bundle state) {
        super.onCreate(state);

        web = new WebView(this);
        web.setBackgroundColor(Color.parseColor("#0e1116"));

        WebSettings settings = web.getSettings();
        settings.setJavaScriptEnabled(true);
        settings.setDomStorageEnabled(true);
        settings.setAllowFileAccess(true);
        settings.setAllowContentAccess(true);
        settings.setMediaPlaybackRequiresUserGesture(false);
        settings.setLoadWithOverviewMode(true);
        settings.setUseWideViewPort(true);
        settings.setSupportZoom(false);
        settings.setBuiltInZoomControls(false);
        settings.setDisplayZoomControls(false);
        settings.setJavaScriptCanOpenWindowsAutomatically(false);

        web.addJavascriptInterface(new Bridge(), "AndroidBridge");

        web.setWebViewClient(new WebViewClient() {
            @Override
            public boolean shouldOverrideUrlLoading(WebView view, WebResourceRequest request) {
                Uri uri = request.getUrl();
                String scheme = uri.getScheme() == null ? "" : uri.getScheme();
                if ("file".equals(scheme) || "about".equals(scheme) || "data".equals(scheme)) {
                    return false;
                }
                openExternal(uri);
                return true;
            }
        });

        web.setWebChromeClient(new WebChromeClient() {
            @Override
            public boolean onShowFileChooser(WebView view, ValueCallback<Uri[]> callback,
                                             FileChooserParams params) {
                if (fileCallback != null) {
                    fileCallback.onReceiveValue(null);
                }
                fileCallback = callback;
                Intent intent = new Intent(Intent.ACTION_GET_CONTENT);
                intent.addCategory(Intent.CATEGORY_OPENABLE);
                intent.setType("*/*");
                intent.putExtra(Intent.EXTRA_MIME_TYPES, new String[]{"video/*", "image/*"});
                try {
                    startActivityForResult(
                            Intent.createChooser(intent, "Выберите видео или картинку"), REQ_FILE);
                    return true;
                } catch (ActivityNotFoundException e) {
                    fileCallback = null;
                    return false;
                }
            }
        });

        setContentView(web);
        applyBarColor(Color.parseColor("#0e1116"));
        web.loadUrl("file:///android_asset/index.html");

        if (Build.VERSION.SDK_INT < 29
                && checkSelfPermission(Manifest.permission.WRITE_EXTERNAL_STORAGE)
                != PackageManager.PERMISSION_GRANTED) {
            requestPermissions(
                    new String[]{Manifest.permission.WRITE_EXTERNAL_STORAGE}, REQ_STORAGE);
        }
    }

    @Override
    protected void onActivityResult(int requestCode, int resultCode, Intent data) {
        if (requestCode == REQ_FILE) {
            Uri[] result = null;
            if (resultCode == RESULT_OK && data != null) {
                if (data.getClipData() != null) {
                    int count = data.getClipData().getItemCount();
                    result = new Uri[count];
                    for (int i = 0; i < count; i++) {
                        result[i] = data.getClipData().getItemAt(i).getUri();
                    }
                } else if (data.getData() != null) {
                    result = new Uri[]{data.getData()};
                }
            }
            if (fileCallback != null) {
                fileCallback.onReceiveValue(result);
                fileCallback = null;
            }
            return;
        }
        super.onActivityResult(requestCode, resultCode, data);
    }

    @Override
    public void onBackPressed() {
        if (web == null) {
            finish();
            return;
        }
        web.evaluateJavascript(
                "(window.__handleBack && window.__handleBack()) ? '1' : '0'",
                new ValueCallback<String>() {
                    @Override
                    public void onReceiveValue(String value) {
                        if (value != null && value.indexOf("1") >= 0) {
                            return;
                        }
                        if (web != null && web.canGoBack()) {
                            web.goBack();
                        } else {
                            finish();
                        }
                    }
                });
    }

    @Override
    protected void onPause() {
        if (web != null) web.onPause();
        super.onPause();
    }

    @Override
    protected void onResume() {
        super.onResume();
        if (web != null) web.onResume();
    }

    @Override
    protected void onDestroy() {
        if (web != null) {
            web.removeJavascriptInterface("AndroidBridge");
            web.destroy();
            web = null;
        }
        super.onDestroy();
    }

    private void openExternal(Uri uri) {
        try {
            startActivity(new Intent(Intent.ACTION_VIEW, uri));
        } catch (ActivityNotFoundException e) {
            toast("Нет приложения для этой ссылки");
        }
    }

    private void toast(final String text) {
        runOnUiThread(new Runnable() {
            @Override
            public void run() {
                Toast.makeText(MainActivity.this, text, Toast.LENGTH_SHORT).show();
            }
        });
    }

    private void js(final String code) {
        runOnUiThread(new Runnable() {
            @Override
            public void run() {
                if (web != null) {
                    web.evaluateJavascript(code, null);
                }
            }
        });
    }

    private static String q(String text) {
        return text == null ? "\"\"" : JSONObject.quote(text);
    }

    private void status(String text, String kind) {
        js("window.__androidStatus && window.__androidStatus(" + q(text) + "," + q(kind) + ")");
    }

    private void applyBarColor(int color) {
        try {
            getWindow().setStatusBarColor(color);
            getWindow().setNavigationBarColor(color);
            boolean light = luminance(color) > 0.6;
            View decor = getWindow().getDecorView();
            if (Build.VERSION.SDK_INT >= 30) {
                WindowInsetsController ctl = decor.getWindowInsetsController();
                if (ctl != null) {
                    int mask = WindowInsetsController.APPEARANCE_LIGHT_STATUS_BARS
                            | WindowInsetsController.APPEARANCE_LIGHT_NAVIGATION_BARS;
                    ctl.setSystemBarsAppearance(light ? mask : 0, mask);
                }
            } else {
                int flags = decor.getSystemUiVisibility();
                if (light) {
                    flags |= View.SYSTEM_UI_FLAG_LIGHT_STATUS_BAR;
                    if (Build.VERSION.SDK_INT >= 26) {
                        flags |= View.SYSTEM_UI_FLAG_LIGHT_NAVIGATION_BAR;
                    }
                } else {
                    flags &= ~View.SYSTEM_UI_FLAG_LIGHT_STATUS_BAR;
                    if (Build.VERSION.SDK_INT >= 26) {
                        flags &= ~View.SYSTEM_UI_FLAG_LIGHT_NAVIGATION_BAR;
                    }
                }
                decor.setSystemUiVisibility(flags);
            }
        } catch (Exception e) {
            Log.w(TAG, "bar color failed", e);
        }
    }

    private static double luminance(int color) {
        return (0.2126 * Color.red(color) + 0.7152 * Color.green(color)
                + 0.0722 * Color.blue(color)) / 255.0;
    }

    private HttpURLConnection open(String url, String method, String body) throws IOException {
        String current = url;
        for (int hop = 0; hop < 6; hop++) {
            HttpURLConnection conn = (HttpURLConnection) new URL(current).openConnection();
            conn.setConnectTimeout(20000);
            conn.setReadTimeout(30000);
            conn.setInstanceFollowRedirects(false);
            conn.setRequestProperty("User-Agent", UA);
            conn.setRequestProperty("Accept", "*/*");
            if ("POST".equalsIgnoreCase(method)) {
                conn.setRequestMethod("POST");
                conn.setDoOutput(true);
                if (body != null && body.length() > 0) {
                    conn.setRequestProperty("Content-Type", "application/json");
                    OutputStream out = conn.getOutputStream();
                    out.write(body.getBytes(StandardCharsets.UTF_8));
                    out.close();
                }
            }
            int code = conn.getResponseCode();
            if (code >= 300 && code < 400) {
                String location = conn.getHeaderField("Location");
                if (location == null) {
                    return conn;
                }
                current = new URL(new URL(current), location).toString();
                conn.disconnect();
                continue;
            }
            return conn;
        }
        throw new IOException("слишком много перенаправлений");
    }

    private static byte[] readAll(InputStream in, long limit) throws IOException {
        ByteArrayOutputStream out = new ByteArrayOutputStream();
        byte[] buffer = new byte[16384];
        long total = 0;
        int read;
        try {
            while ((read = in.read(buffer)) > 0) {
                total += read;
                if (limit > 0 && total > limit) {
                    throw new IOException("слишком большой ответ");
                }
                out.write(buffer, 0, read);
            }
        } finally {
            in.close();
        }
        return out.toByteArray();
    }

    private static String sanitize(String name) {
        String value = name == null ? "" : name;
        value = value.replaceAll("[\\\\/:*?\"<>|]+", " ").replaceAll("\\s+", " ").trim();
        if (value.length() > 80) {
            value = value.substring(0, 80).trim();
        }
        return value.isEmpty() ? "video2gif" : value;
    }

    private static String guessMime(String contentType, String name) {
        if (contentType != null) {
            String type = contentType.toLowerCase(Locale.US).trim();
            int semi = type.indexOf(';');
            if (semi > 0) type = type.substring(0, semi).trim();
            if (type.startsWith("video/") || type.startsWith("image/")) {
                return type;
            }
        }
        String lower = name == null ? "" : name.toLowerCase(Locale.US);
        if (lower.endsWith(".gif")) return "image/gif";
        if (lower.endsWith(".png")) return "image/png";
        if (lower.endsWith(".jpg") || lower.endsWith(".jpeg")) return "image/jpeg";
        if (lower.endsWith(".webm")) return "video/webm";
        return "video/mp4";
    }

    private static String withExt(String name, String mime) {
        String lower = name.toLowerCase(Locale.US);
        if (lower.matches(".*\\.(mp4|m4v|webm|mov|mkv|gif|png|jpe?g)$")) {
            return name;
        }
        String ext = ".mp4";
        if ("image/gif".equals(mime)) ext = ".gif";
        else if ("image/png".equals(mime)) ext = ".png";
        else if ("image/jpeg".equals(mime)) ext = ".jpg";
        else if ("video/webm".equals(mime)) ext = ".webm";
        return name + ext;
    }

    private static boolean isImage(String mime) {
        return mime != null && mime.startsWith("image/");
    }

    private static String galleryDir(String mime) {
        return (isImage(mime) ? "Pictures" : "Movies") + "/Video2Gif";
    }

    private static Uri galleryCollection(String mime) {
        return isImage(mime) ? MediaStore.Images.Media.EXTERNAL_CONTENT_URI
                : MediaStore.Video.Media.EXTERNAL_CONTENT_URI;
    }

    private String saveStream(InputStream in, String name, String mime) throws IOException {
        if (Build.VERSION.SDK_INT >= 29) {
            ContentValues values = new ContentValues();
            values.put(MediaStore.MediaColumns.DISPLAY_NAME, name);
            values.put(MediaStore.MediaColumns.MIME_TYPE, mime);
            values.put(MediaStore.MediaColumns.RELATIVE_PATH, galleryDir(mime));
            values.put(MediaStore.MediaColumns.IS_PENDING, 1);
            Uri uri = getContentResolver().insert(galleryCollection(mime), values);
            if (uri == null) {
                throw new IOException("не удалось создать файл в галерее");
            }
            OutputStream out = getContentResolver().openOutputStream(uri);
            if (out == null) {
                getContentResolver().delete(uri, null, null);
                throw new IOException("нет доступа к галерее");
            }
            try {
                copy(in, out);
            } catch (IOException e) {
                getContentResolver().delete(uri, null, null);
                throw e;
            }
            ContentValues done = new ContentValues();
            done.put(MediaStore.MediaColumns.IS_PENDING, 0);
            getContentResolver().update(uri, done, null, null);
            return name;
        }
        File dir = new File(Environment.getExternalStoragePublicDirectory(
                isImage(mime) ? Environment.DIRECTORY_PICTURES : Environment.DIRECTORY_MOVIES), "Video2Gif");
        if (!dir.exists() && !dir.mkdirs()) {
            throw new IOException("нет доступа к галерее");
        }
        File file = uniqueFile(dir, name);
        copy(in, new FileOutputStream(file));
        scanMedia(file, mime);
        return file.getName();
    }

    private void scanMedia(File file, String mime) {
        try {
            MediaScannerConnection.scanFile(this, new String[]{file.getAbsolutePath()},
                    new String[]{mime}, null);
        } catch (Exception ignored) {
        }
    }

    private static File uniqueFile(File dir, String name) {
        File file = new File(dir, name);
        if (!file.exists()) {
            return file;
        }
        int dot = name.lastIndexOf('.');
        String base = dot > 0 ? name.substring(0, dot) : name;
        String ext = dot > 0 ? name.substring(dot) : "";
        for (int i = 1; i < 1000; i++) {
            File candidate = new File(dir, base + " (" + i + ")" + ext);
            if (!candidate.exists()) {
                return candidate;
            }
        }
        return file;
    }

    private static void copy(InputStream in, OutputStream out) throws IOException {
        byte[] buffer = new byte[65536];
        try {
            int read;
            while ((read = in.read(buffer)) > 0) {
                out.write(buffer, 0, read);
            }
            out.flush();
        } finally {
            try {
                in.close();
            } catch (IOException ignored) {
            }
            try {
                out.close();
            } catch (IOException ignored) {
            }
        }
    }

    private static String msg(Throwable error) {
        String text = error == null ? null : error.getMessage();
        return text == null || text.isEmpty() ? "ошибка сети" : text;
    }

    public class Bridge {

        @JavascriptInterface
        public void toast(String text) {
            MainActivity.this.toast(text);
        }

        @JavascriptInterface
        public void setThemeColor(final String color) {
            runOnUiThread(new Runnable() {
                @Override
                public void run() {
                    try {
                        applyBarColor(Color.parseColor(color));
                    } catch (Exception ignored) {
                    }
                }
            });
        }

        @JavascriptInterface
        public void openUrl(final String url) {
            runOnUiThread(new Runnable() {
                @Override
                public void run() {
                    try {
                        openExternal(Uri.parse(url));
                    } catch (Exception e) {
                        toast("Не удалось открыть ссылку");
                    }
                }
            });
        }

        @JavascriptInterface
        public void httpGet(final String id, final String url, final String method,
                            final String body) {
            new Thread(new Runnable() {
                @Override
                public void run() {
                    HttpURLConnection conn = null;
                    boolean ok = false;
                    int statusCode = 0;
                    String text = "";
                    String ctype = "";
                    try {
                        conn = open(url, method, body);
                        int code = conn.getResponseCode();
                        statusCode = code;
                        ctype = conn.getContentType() == null ? "" : conn.getContentType();
                        InputStream in = code >= 400 ? conn.getErrorStream() : conn.getInputStream();
                        if (in == null) {
                            throw new IOException("HTTP " + code);
                        }
                        text = new String(readAll(in, MAX_TEXT), StandardCharsets.UTF_8);
                        ok = code >= 200 && code < 300;
                        if (!ok && text.isEmpty()) {
                            text = "HTTP " + code;
                        }
                    } catch (Exception e) {
                        text = msg(e);
                    } finally {
                        if (conn != null) conn.disconnect();
                    }
                    js("window.__androidHttp && window.__androidHttp(" + q(id) + ","
                            + (ok ? "true" : "false") + "," + statusCode + ","
                            + q(text) + "," + q(ctype) + ")");
                }
            }).start();
        }

        @JavascriptInterface
        public void downloadUrl(final String url, final String name) {
            status("Скачиваем видео…", "");
            new Thread(new Runnable() {
                @Override
                public void run() {
                    HttpURLConnection conn = null;
                    try {
                        conn = open(url, "GET", null);
                        int code = conn.getResponseCode();
                        if (code < 200 || code >= 300) {
                            throw new IOException("HTTP " + code);
                        }
                        String type = conn.getContentType();
                        String mime = guessMime(type, name);
                        String fileName = withExt(sanitize(name), mime);
                        InputStream in = conn.getInputStream();
                        if (in == null) {
                            throw new IOException("пустой ответ");
                        }
                        String saved = saveStream(in, fileName, mime);
                        status("Сохранено в галерею: " + saved, "ok");
                        toast("Сохранено в галерею: " + saved);
                    } catch (Exception e) {
                        status("Не удалось скачать: " + msg(e), "err");
                    } finally {
                        if (conn != null) conn.disconnect();
                    }
                }
            }).start();
        }

        @JavascriptInterface
        public void saveBegin(String id, String name, String mime) {
            try {
                String safeMime = guessMime(mime, name);
                String fileName = withExt(sanitize(name), safeMime);
                synchronized (chunkLock) {
                    if (chunkStreams.containsKey(id)) {
                        abortChunk(id, null);
                    }
                    if (Build.VERSION.SDK_INT >= 29) {
                        ContentValues values = new ContentValues();
                        values.put(MediaStore.MediaColumns.DISPLAY_NAME, fileName);
                        values.put(MediaStore.MediaColumns.MIME_TYPE, safeMime);
                        values.put(MediaStore.MediaColumns.RELATIVE_PATH, galleryDir(safeMime));
                        values.put(MediaStore.MediaColumns.IS_PENDING, 1);
                        Uri uri = getContentResolver().insert(galleryCollection(safeMime), values);
                        if (uri == null) {
                            throw new IOException("не удалось создать файл в галерее");
                        }
                        OutputStream out = getContentResolver().openOutputStream(uri);
                        if (out == null) {
                            getContentResolver().delete(uri, null, null);
                            throw new IOException("нет доступа к галерее");
                        }
                        chunkStreams.put(id, out);
                        chunkUris.put(id, uri);
                    } else {
                        File dir = new File(Environment.getExternalStoragePublicDirectory(
                                isImage(safeMime) ? Environment.DIRECTORY_PICTURES : Environment.DIRECTORY_MOVIES),
                                "Video2Gif");
                        if (!dir.exists() && !dir.mkdirs()) {
                            throw new IOException("нет доступа к галерее");
                        }
                        File file = uniqueFile(dir, fileName);
                        chunkStreams.put(id, new FileOutputStream(file));
                        chunkFiles.put(id, file);
                    }
                    chunkNames.put(id, fileName);
                    chunkMimes.put(id, safeMime);
                }
            } catch (Exception e) {
                status("Не удалось сохранить: " + msg(e), "err");
            }
        }

        @JavascriptInterface
        public void saveChunk(String id, String data) {
            synchronized (chunkLock) {
                OutputStream out = chunkStreams.get(id);
                if (out == null) {
                    return;
                }
                try {
                    out.write(Base64.decode(data, Base64.DEFAULT));
                } catch (Exception e) {
                    abortChunk(id, e);
                }
            }
        }

        @JavascriptInterface
        public void saveEnd(String id) {
            String saved = null;
            String mime = null;
            Uri uri = null;
            File file = null;
            Exception failure = null;
            synchronized (chunkLock) {
                OutputStream out = chunkStreams.remove(id);
                if (out == null) {
                    return;
                }
                try {
                    out.flush();
                    out.close();
                } catch (Exception e) {
                    failure = e;
                }
                uri = chunkUris.remove(id);
                file = chunkFiles.remove(id);
                mime = chunkMimes.remove(id);
                saved = chunkNames.remove(id);
                if (failure != null && file != null) {
                    file.delete();
                }
            }
            if (failure != null) {
                if (uri != null) {
                    try {
                        getContentResolver().delete(uri, null, null);
                    } catch (Exception ignored) {
                    }
                }
                status("Не удалось сохранить: " + msg(failure), "err");
                return;
            }
            if (uri != null && Build.VERSION.SDK_INT >= 29) {
                try {
                    ContentValues done = new ContentValues();
                    done.put(MediaStore.MediaColumns.IS_PENDING, 0);
                    getContentResolver().update(uri, done, null, null);
                } catch (Exception ignored) {
                }
            } else if (file != null) {
                scanMedia(file, mime);
            }
            if (saved != null) {
                status("Сохранено в галерею: " + saved, "ok");
                toast("Сохранено в галерею: " + saved);
            }
        }

        @JavascriptInterface
        public void saveFail(String id) {
            synchronized (chunkLock) {
                abortChunk(id, null);
            }
        }
    }

    private void abortChunk(String id, Throwable error) {
        OutputStream out = chunkStreams.remove(id);
        if (out != null) {
            try {
                out.close();
            } catch (IOException ignored) {
            }
        }
        Uri uri = chunkUris.remove(id);
        if (uri != null) {
            try {
                getContentResolver().delete(uri, null, null);
            } catch (Exception ignored) {
            }
        }
        File file = chunkFiles.remove(id);
        if (file != null) {
            file.delete();
        }
        chunkNames.remove(id);
        chunkMimes.remove(id);
        if (error != null) {
            status("Не удалось сохранить: " + msg(error), "err");
        }
    }
}
