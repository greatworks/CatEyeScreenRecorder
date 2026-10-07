using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace FreeWindowsScreenRecorder
{
    internal enum AppLanguage
    {
        SimplifiedChinese,
        TraditionalChinese,
        English,
        Japanese,
        Korean,
        German,
        French,
        Spanish,
        Portuguese,
        Russian,
        Chinese = SimplifiedChinese
    }

    internal sealed class LanguageOption
    {
        internal readonly AppLanguage Language;
        internal readonly string Code;
        internal readonly string NativeName;
        internal readonly string ShortCode;
        internal LanguageOption(AppLanguage language, string code, string nativeName, string shortCode)
        { Language = language; Code = code; NativeName = nativeName; ShortCode = shortCode; }
        public override string ToString() { return NativeName; }
    }

    internal static class Localization
    {
        internal static readonly LanguageOption[] Languages = new LanguageOption[]
        {
            new LanguageOption(AppLanguage.SimplifiedChinese, "zh-CN", "简体中文", "简中"),
            new LanguageOption(AppLanguage.TraditionalChinese, "zh-TW", "繁體中文", "繁中"),
            new LanguageOption(AppLanguage.English, "en", "English", "EN"),
            new LanguageOption(AppLanguage.Japanese, "ja", "日本語", "日本語"),
            new LanguageOption(AppLanguage.Korean, "ko", "한국어", "한국어"),
            new LanguageOption(AppLanguage.German, "de", "Deutsch", "DE"),
            new LanguageOption(AppLanguage.French, "fr", "Français", "FR"),
            new LanguageOption(AppLanguage.Spanish, "es", "Español", "ES"),
            new LanguageOption(AppLanguage.Portuguese, "pt", "Português", "PT"),
            new LanguageOption(AppLanguage.Russian, "ru", "Русский", "RU")
        };
        private static readonly Dictionary<string, string[]> translations = BuildTranslations();
        internal static AppLanguage Current = AppLanguage.SimplifiedChinese;
        internal static bool IsEnglish { get { return Current == AppLanguage.English; } }
        internal static string Code { get { return Find(Current).Code; } }
        internal static string ShortCode { get { return Find(Current).ShortCode; } }
        internal static string Text(string chinese, string english)
        {
            if (Current == AppLanguage.SimplifiedChinese) return chinese;
            if (Current == AppLanguage.TraditionalChinese && (chinese == "猫眼" || chinese == "猫眼录屏" || chinese == "猫眼录屏  /  CATEYE")) return chinese;
            if (Current == AppLanguage.English) return english;
            string[] values;
            int translationIndex = (int)Current - 1; // Add() stores non-Simplified-Chinese values: zh-TW, en, ja, ko, de, fr, es, pt, ru.
            return translations.TryGetValue(english, out values) && translationIndex >= 0 && translationIndex < values.Length ? values[translationIndex] : english;
        }
        internal static string Format(string chinese, string english, params object[] args) { return String.Format(Text(chinese, english), args); }
        internal static void SetFromCode(string code)
        {
            if (String.IsNullOrEmpty(code)) { Current = AppLanguage.SimplifiedChinese; return; }
            string normalized = code.Trim().ToLowerInvariant();
            if (normalized == "zh" || normalized == "zh-cn") { Current = AppLanguage.SimplifiedChinese; return; }
            if (normalized == "tw" || normalized == "zh-tw" || normalized == "zh-hk") { Current = AppLanguage.TraditionalChinese; return; }
            for (int i = 0; i < Languages.Length; i++) if (Languages[i].Code == normalized) { Current = Languages[i].Language; return; }
            Current = AppLanguage.SimplifiedChinese;
        }
        internal static LanguageOption Find(AppLanguage language)
        {
            for (int i = 0; i < Languages.Length; i++) if (Languages[i].Language == language) return Languages[i];
            return Languages[0];
        }
        internal static string ProductName { get { return Current == AppLanguage.TraditionalChinese ? "猫眼录屏" : Text("猫眼录屏", "CatEye Screen Recorder"); } }
        internal static string ProductTitle { get { return Current == AppLanguage.TraditionalChinese ? "猫眼录屏 · CatEye Screen Recorder" : Text("猫眼录屏 · CatEye Screen Recorder", "CatEye Screen Recorder"); } }

        private static Dictionary<string, string[]> BuildTranslations()
        {
            Dictionary<string, string[]> map = new Dictionary<string, string[]>(StringComparer.Ordinal);
            Add(map, "CatEye Screen Recorder", "CatEye Screen Recorder", "CatEye Screen Recorder", "CatEye Screen Recorder", "CatEye Screen Recorder", "CatEye Screen Recorder", "CatEye Screen Recorder", "CatEye Screen Recorder", "CatEye Screen Recorder", "CatEye Screen Recorder");
            Add(map, "Free, lightweight and watermark-free. Open and record.", "免費、輕量、無浮水印。開啟即可錄製。", "Free, lightweight and watermark-free. Open and record.", "無料・軽量・透かしなし。開くだけで録画できます。", "무료·가벼움·워터마크 없음. 열고 바로 녹화하세요.", "Kostenlos, leicht und ohne Wasserzeichen. Öffnen und aufnehmen.", "Gratuit, léger et sans filigrane. Ouvrez et enregistrez.", "Gratis, ligero y sin marca de agua. Abre y graba.", "Grátis, leve e sem marca d’água. Abra e grave.", "Бесплатно, легко и без водяных знаков. Откройте и записывайте.");
            Add(map, "Recording workspace", "錄製工作台", "Recording workspace", "録画ワークスペース", "녹화 작업 공간", "Aufnahme-Arbeitsbereich", "Espace d’enregistrement", "Espacio de grabación", "Área de gravação", "Рабочая область записи");
            Add(map, "Green · Lightweight · Ad-free", "綠色 · 輕量 · 無廣告", "Green · Lightweight · Ad-free", "グリーン · 軽量 · 広告なし", "친환경 · 가벼움 · 광고 없음", "Grün · Leicht · Werbefrei", "Vert · Léger · Sans publicité", "Verde · Ligero · Sin anuncios", "Verde · Leve · Sem anúncios", "Зелёный · лёгкий · без рекламы");
            Add(map, "●  HD · No watermark", "●  高清畫面 · 無浮水印", "●  HD · No watermark", "●  HD · 透かしなし", "●  HD · 워터마크 없음", "●  HD · Kein Wasserzeichen", "●  HD · Sans filigrane", "●  HD · Sin marca de agua", "●  HD · Sem marca d’água", "●  HD · Без водяного знака");
            Add(map, "▣  Full screen", "▣  全螢幕", "▣  Full screen", "▣  全画面", "▣  전체 화면", "▣  Vollbild", "▣  Plein écran", "▣  Pantalla completa", "▣  Tela cheia", "▣  Полный экран");
            Add(map, "⌗  Custom area", "⌗  自訂區域", "⌗  Custom area", "⌗  カスタム範囲", "⌗  사용자 지정 영역", "⌗  Benutzerdefinierter Bereich", "⌗  Zone personnalisée", "⌗  Área personalizada", "⌗  Área personalizada", "⌗  Пользовательская область");
            Add(map, "Select again", "重新框選", "Select again", "再選択", "다시 선택", "Erneut auswählen", "Sélectionner à nouveau", "Seleccionar de nuevo", "Selecionar novamente", "Выбрать снова");
            Add(map, "Screen recording", "螢幕錄製", "Screen recording", "画面録画", "화면 녹화", "Bildschirmaufnahme", "Enregistrement d’écran", "Grabación de pantalla", "Gravação de tela", "Запись экрана");
            Add(map, "▣   Screen recording", "▣   螢幕錄製", "▣   Screen recording", "▣   画面録画", "▣   화면 녹화", "▣   Bildschirmaufnahme", "▣   Enregistrement d’écran", "▣   Grabación de pantalla", "▣   Gravação de tela", "▣   Запись экрана");
            Add(map, "Recordings", "錄製檔案夾", "Recordings", "録画フォルダー", "녹화 폴더", "Aufnahmen", "Enregistrements", "Grabaciones", "Gravações", "Записи");
            Add(map, "▤   Recordings", "▤   錄製檔案夾", "▤   Recordings", "▤   録画フォルダー", "▤   녹화 폴더", "▤   Aufnahmen", "▤   Enregistrements", "▤   Grabaciones", "▤   Gravações", "▤   Записи");
            Add(map, "ⓘ   About CatEye", "ⓘ   關於猫眼", "ⓘ   About CatEye", "ⓘ   CatEyeについて", "ⓘ   CatEye 정보", "ⓘ   Über CatEye", "ⓘ   À propos de CatEye", "ⓘ   Acerca de CatEye", "ⓘ   Sobre o CatEye", "ⓘ   О CatEye");
            Add(map, "Free · No watermark", "免費 · 無浮水印", "Free · No watermark", "無料 · 透かしなし", "무료 · 워터마크 없음", "Kostenlos · Kein Wasserzeichen", "Gratuit · Sans filigrane", "Gratis · Sin marca de agua", "Grátis · Sem marca d’água", "Бесплатно · Без водяного знака");
            Add(map, "Local recording\nClear & light", "本機錄製\n清晰 · 輕巧", "Local recording\nClear & light", "ローカル録画\nクリアで軽量", "로컬 녹화\n선명하고 가벼움", "Lokale Aufnahme\nKlar und leicht", "Enregistrement local\nClair et léger", "Grabación local\nClara y ligera", "Gravação local\nClara e leve", "Локальная запись\nЧётко и легко");
            Add(map, "Quality & size", "畫質與體積", "Quality & size", "画質とサイズ", "화질 및 크기", "Qualität & Größe", "Qualité et taille", "Calidad y tamaño", "Qualidade e tamanho", "Качество и размер");
            Add(map, "HQ MP4 · Compact", "高畫質 MP4 · 省空間", "HQ MP4 · Compact", "高画質 MP4 · コンパクト", "고화질 MP4 · 컴팩트", "HQ MP4 · Kompakt", "MP4 HD · Compact", "MP4 HD · Compacto", "MP4 HD · Compacto", "HQ MP4 · Компактный");
            Add(map, "Lossless MKV", "原畫無損 MKV", "Lossless MKV", "ロスレス MKV", "무손실 MKV", "Verlustfreies MKV", "MKV sans perte", "MKV sin pérdida", "MKV sem perdas", "Без потерь MKV");
            Add(map, "Native resolution · High quality", "原始解析度 · 高品質壓縮", "Native resolution · High quality", "元の解像度 · 高品質", "원본 해상도 · 고품질", "Native Auflösung · Hohe Qualität", "Résolution native · Haute qualité", "Resolución nativa · Alta calidad", "Resolução nativa · Alta qualidade", "Исходное разрешение · высокое качество");
            Add(map, "Pixel-perfect · Larger file", "逐像素保真 · 檔案較大", "Pixel-perfect · Larger file", "ピクセル完全 · 大きなファイル", "픽셀 완벽 · 큰 파일", "Pixelgenau · Größere Datei", "Pixel parfait · Fichier volumineux", "Píxel perfecto · Archivo grande", "Pixel perfeito · Arquivo maior", "Пиксельная точность · большой файл");
            Add(map, "Frame rate", "幀率", "Frame rate", "フレームレート", "프레임 속도", "Bildrate", "Fréquence d’images", "Frecuencia de fotogramas", "Taxa de quadros", "Частота кадров");
            Add(map, "15 FPS · Docs", "15 FPS · 文件示範", "15 FPS · Docs", "15 FPS · 文書", "15 FPS · 문서", "15 FPS · Dokumente", "15 FPS · Documents", "15 FPS · Documentos", "15 FPS · Documentos", "15 FPS · Документы");
            Add(map, "30 FPS · Daily", "30 FPS · 日常錄製", "30 FPS · Daily", "30 FPS · 日常録画", "30 FPS · 일상", "30 FPS · Alltag", "30 FPS · Quotidien", "30 FPS · Diario", "30 FPS · Diário", "30 FPS · Повседневная запись");
            Add(map, "60 FPS · Smooth", "60 FPS · 流暢動態", "60 FPS · Smooth", "60 FPS · スムーズ", "60 FPS · 부드러운 동작", "60 FPS · Flüssig", "60 FPS · Fluide", "60 FPS · Fluido", "60 FPS · Suave", "60 FPS · Плавно");
            Add(map, "Save to", "儲存至", "Save to", "保存先", "저장 위치", "Speichern unter", "Enregistrer dans", "Guardar en", "Salvar em", "Сохранить в");
            Add(map, "Change folder", "變更資料夾", "Change folder", "フォルダーを変更", "폴더 변경", "Ordner ändern", "Changer de dossier", "Cambiar carpeta", "Alterar pasta", "Изменить папку");
            Add(map, "Ready · Controls stay hidden", "就緒 · 控制列不入鏡", "Ready · Controls stay hidden", "準備完了 · 操作バーは録画されません", "준비됨 · 컨트롤은 녹화되지 않음", "Bereit · Steuerelemente bleiben verborgen", "Prêt · Les commandes restent masquées", "Listo · Controles ocultos", "Pronto · Controles ocultos", "Готово · элементы управления скрыты");
            Add(map, "F9 pause / resume    F10 stop", "F9 暫停 / 繼續    F10 停止", "F9 pause / resume    F10 stop", "F9 一時停止 / 再開    F10 停止", "F9 일시정지 / 재개    F10 중지", "F9 Pause / Fortsetzen    F10 Stopp", "F9 pause / reprendre    F10 arrêter", "F9 pausar / reanudar    F10 detener", "F9 pausar / retomar    F10 parar", "F9 пауза / продолжить    F10 стоп");
            Add(map, "●  Start recording", "●  開始錄製", "●  Start recording", "●  録画開始", "●  녹화 시작", "●  Aufnahme starten", "●  Démarrer", "●  Iniciar grabación", "●  Iniciar gravação", "●  Начать запись");
            Add(map, "Click to play  ", "點擊播放  ", "Click to play  ", "クリックして再生  ", "클릭하여 재생  ", "Zum Abspielen klicken  ", "Cliquer pour lire  ", "Haz clic para reproducir  ", "Clique para reproduzir  ", "Нажмите для воспроизведения  ");
            Add(map, "Full-screen recording", "全螢幕錄製", "Full-screen recording", "全画面録画", "전체 화면 녹화", "Vollbildaufnahme", "Enregistrement plein écran", "Grabación a pantalla completa", "Gravação em tela cheia", "Полноэкранная запись");
            Add(map, "Area recording  (", "區域錄製  (", "Area recording  (", "範囲録画  (", "영역 녹화  (", "Bereichsaufnahme  (", "Enregistrement de zone  (", "Grabación de área  (", "Gravação de área  (", "Запись области  (");
            Add(map, "Native resolution", "原始解析度", "Native resolution", "元の解像度", "원본 해상도", "Native Auflösung", "Résolution native", "Resolución nativa", "Resolução nativa", "Исходное разрешение");
            Add(map, "Close", "關閉", "Close", "閉じる", "닫기", "Schließen", "Fermer", "Cerrar", "Fechar", "Закрыть");
            Add(map, "About", "關於", "About", "概要", "정보", "Über", "À propos", "Acerca de", "Sobre", "О программе");
            Add(map, "Key features", "核心特色", "Key features", "主な機能", "주요 기능", "Funktionen", "Fonctionnalités", "Características", "Recursos", "Основные возможности");
            Add(map, "Use cases", "適用情境", "Use cases", "用途", "사용 사례", "Einsatzbereiche", "Cas d’usage", "Usos", "Casos de uso", "Сценарии использования");
            Add(map, "Pricing: Free\nEnvironment: Windows 7 / 10 / 11", "收費模式：完全免費\n環境：Windows 7 / 10 / 11", "Pricing: Free\nEnvironment: Windows 7 / 10 / 11", "料金：無料\n環境：Windows 7 / 10 / 11", "요금: 무료\n환경: Windows 7 / 10 / 11", "Preis: Kostenlos\nUmgebung: Windows 7 / 10 / 11", "Prix : gratuit\nEnvironnement : Windows 7 / 10 / 11", "Precio: Gratis\nEntorno: Windows 7 / 10 / 11", "Preço: Grátis\nAmbiente: Windows 7 / 10 / 11", "Цена: бесплатно\nСреда: Windows 7 / 10 / 11");
            Add(map, "• No watermark\n• Lightweight\n• Completely free\n• Windows 7 / 10 / 11", "• 無浮水印\n• 輕量低佔用\n• 完全免費\n• Windows 7 / 10 / 11", "• No watermark\n• Lightweight\n• Completely free\n• Windows 7 / 10 / 11", "• 透かしなし\n• 軽量\n• 完全無料\n• Windows 7 / 10 / 11", "• 워터마크 없음\n• 가벼운 사용량\n• 완전 무료\n• Windows 7 / 10 / 11", "• Kein Wasserzeichen\n• Leichtgewichtig\n• Komplett kostenlos\n• Windows 7 / 10 / 11", "• Sans filigrane\n• Léger\n• Entièrement gratuit\n• Windows 7 / 10 / 11", "• Sin marca de agua\n• Ligero\n• Totalmente gratis\n• Windows 7 / 10 / 11", "• Sem marca d’água\n• Leve\n• Totalmente grátis\n• Windows 7 / 10 / 11", "• Без водяных знаков\n• Лёгкий\n• Полностью бесплатно\n• Windows 7 / 10 / 11");
            Add(map, "Pause recording", "暫停錄製", "Pause recording", "録画を一時停止", "녹화 일시정지", "Aufnahme pausieren", "Mettre en pause", "Pausar grabación", "Pausar gravação", "Приостановить запись");
            Add(map, "Stop and save", "停止並儲存", "Stop and save", "停止して保存", "중지 및 저장", "Stoppen und speichern", "Arrêter et enregistrer", "Detener y guardar", "Parar e salvar", "Остановить и сохранить");
            Add(map, "Resume recording", "繼續錄製", "Resume recording", "録画を再開", "녹화 재개", "Aufnahme fortsetzen", "Reprendre", "Reanudar grabación", "Retomar gravação", "Продолжить запись");
            Add(map, "Software introduction", "軟體簡介", "Software introduction", "ソフトウェア紹介", "소프트웨어 소개", "Softwarevorstellung", "Présentation du logiciel", "Introducción del software", "Introdução do software", "О программе");
            Add(map, "A free Windows recorder with no watermark, low resource use and quick setup. Open the app and start.", "一款免費的 Windows 錄屏工具，無浮水印、低佔用、設定簡單。開啟即可開始。", "A free Windows recorder with no watermark, low resource use and quick setup. Open the app and start.", "透かしなし・低負荷・簡単設定の無料 Windows 録画ソフトです。開いてすぐ開始できます。", "워터마크 없이 가볍고 설정이 간단한 무료 Windows 녹화 도구입니다. 열고 바로 시작하세요.", "Kostenloser Windows-Recorder ohne Wasserzeichen, mit geringer Last und einfacher Einrichtung. Öffnen und starten.", "Enregistreur Windows gratuit, sans filigrane, léger et simple à configurer. Ouvrez et commencez.", "Grabador de Windows gratuito, ligero y sin marca de agua. Configuración rápida: abre y empieza.", "Gravador gratuito para Windows, leve e sem marca d’água. Configuração simples: abra e comece.", "Бесплатный рекордер Windows без водяных знаков, лёгкий и простой. Откройте и начните.");
            Add(map, "Classes, games, meetings, tutorials and daily screen records", "課程、遊戲、會議、教學與日常螢幕記錄", "Classes, games, meetings, tutorials and daily screen records", "授業、ゲーム、会議、チュートリアル、日常の画面記録", "수업, 게임, 회의, 튜토리얼 및 일상 화면 기록", "Kurse, Spiele, Meetings, Tutorials und tägliche Bildschirmaufnahmen", "Cours, jeux, réunions, tutoriels et captures quotidiennes", "Clases, juegos, reuniones, tutoriales y grabaciones diarias", "Aulas, jogos, reuniões, tutoriais e registros diários", "Занятия, игры, встречи, обучение и повседневная запись экрана");
            Add(map, "The encoder is missing. Keep the tools folder beside the program.", "找不到編碼元件，請將 tools 資料夾與程式放在一起。", "The encoder is missing. Keep the tools folder beside the program.", "エンコーダーがありません。tools フォルダーをプログラムの隣に置いてください。", "인코더가 없습니다. 프로그램 옆에 tools 폴더를 두세요.", "Encoder fehlt. Lassen Sie den tools-Ordner neben dem Programm.", "Encodeur introuvable. Placez le dossier tools à côté du programme.", "Falta el codificador. Mantén la carpeta tools junto al programa.", "O codificador está ausente. Mantenha a pasta tools ao lado do programa.", "Кодировщик не найден. Поместите папку tools рядом с программой.");
            Add(map, "Cannot save to this folder", "無法儲存至此資料夾", "Cannot save to this folder", "このフォルダーに保存できません", "이 폴더에 저장할 수 없음", "Speichern in diesen Ordner nicht möglich", "Impossible d’enregistrer dans ce dossier", "No se puede guardar en esta carpeta", "Não é possível salvar nesta pasta", "Невозможно сохранить в эту папку");
            Add(map, "Choose where recordings are saved", "選擇錄製檔案儲存位置", "Choose where recordings are saved", "録画の保存先を選択", "녹화 저장 위치 선택", "Speicherort für Aufnahmen wählen", "Choisir le dossier d’enregistrement", "Elige dónde guardar las grabaciones", "Escolha onde salvar as gravações", "Выберите папку для записей");
            Add(map, "Cannot open folder", "無法開啟資料夾", "Cannot open folder", "フォルダーを開けません", "폴더를 열 수 없음", "Ordner kann nicht geöffnet werden", "Impossible d’ouvrir le dossier", "No se puede abrir la carpeta", "Não é possível abrir a pasta", "Невозможно открыть папку");
            Add(map, "If a shortcut is busy, use the floating bar or tray controls.", "快捷鍵被佔用時，請使用懸浮列或系統匣控制。", "If a shortcut is busy, use the floating bar or tray controls.", "ショートカットが使用中の場合は、操作バーまたはトレイを使ってください。", "단축키가 사용 중이면 플로팅 바 또는 트레이를 사용하세요.", "Wenn ein Tastenkürzel belegt ist, verwenden Sie Leiste oder Tray.", "Si un raccourci est utilisé, utilisez la barre ou la zone de notification.", "Si un atajo está ocupado, usa la barra flotante o la bandeja.", "Se um atalho estiver ocupado, use a barra flutuante ou a bandeja.", "Если сочетание занято, используйте плавающую панель или трей.");
            Add(map, "CatEye Screen Recorder · Select area", "猫眼录屏 · 選擇區域", "CatEye Screen Recorder · Select area", "CatEye Screen Recorder · 範囲選択", "CatEye Screen Recorder · 영역 선택", "CatEye Screen Recorder · Bereich auswählen", "CatEye Screen Recorder · Sélectionner une zone", "CatEye Screen Recorder · Seleccionar área", "CatEye Screen Recorder · Selecionar área", "CatEye Screen Recorder · Выбор области");
            Add(map, "Drag to select a rectangle   ·   Esc or right-click to cancel", "拖曳選取矩形區域   ·   Esc 或右鍵取消", "Drag to select a rectangle   ·   Esc or right-click to cancel", "ドラッグして矩形を選択   ·   Esc または右クリックでキャンセル", "드래그하여 사각형 선택   ·   Esc 또는 오른쪽 클릭으로 취소", "Ziehen Sie ein Rechteck   ·   Esc oder Rechtsklick zum Abbrechen", "Faites glisser pour sélectionner un rectangle   ·   Échap ou clic droit pour annuler", "Arrastra para seleccionar un rectángulo   ·   Esc o clic derecho para cancelar", "Arraste para selecionar um retângulo   ·   Esc ou clique direito para cancelar", "Перетащите, чтобы выбрать прямоугольник   ·   Esc или щёлкните правой кнопкой для отмены");
            Add(map, " pixels   ·   release to finish", " 像素   ·   放開滑鼠完成", " pixels   ·   release to finish", " ピクセル   ·   離して完了", " 픽셀   ·   놓으면 완료", " Pixel   ·   loslassen zum Abschluss", " pixels   ·   relâchez pour terminer", " píxeles   ·   suelta para terminar", " pixels   ·   solte para concluir", " пикс.   ·   отпустите для завершения");
            Add(map, "CatEye Screen Recorder · Recording controls", "猫眼录屏 · 錄製控制", "CatEye Screen Recorder · Recording controls", "CatEye Screen Recorder · 録画操作", "CatEye Screen Recorder · 녹화 컨트롤", "CatEye Screen Recorder · Aufnahmesteuerung", "CatEye Screen Recorder · Commandes d’enregistrement", "CatEye Screen Recorder · Controles de grabación", "CatEye Screen Recorder · Controles de gravação", "CatEye Screen Recorder · Управление записью");
            Add(map, "● Recording", "● 錄製中", "● Recording", "● 録画中", "● 녹화 중", "● Aufnahme läuft", "● Enregistrement", "● Grabando", "● Gravando", "● Запись");
            Add(map, "Ⅱ  Pause", "Ⅱ  暫停", "Ⅱ  Pause", "Ⅱ  一時停止", "Ⅱ  일시정지", "Ⅱ  Pause", "Ⅱ  Pause", "Ⅱ  Pausar", "Ⅱ  Pausar", "Ⅱ  Пауза");
            Add(map, "■  Stop", "■  停止", "■  Stop", "■  停止", "■  중지", "■  Stopp", "■  Arrêter", "■  Detener", "■  Parar", "■  Стоп");
            Add(map, "▶  Resume", "▶  繼續", "▶  Resume", "▶  再開", "▶  재개", "▶  Fortsetzen", "▶  Reprendre", "▶  Reanudar", "▶  Retomar", "▶  Продолжить");
            Add(map, "Saving…", "正在儲存…", "Saving…", "保存中…", "저장 중…", "Speichern…", "Enregistrement…", "Guardando…", "Salvando…", "Сохранение…");
            Add(map, "● Paused", "● 已暫停", "● Paused", "● 一時停止", "● 일시정지됨", "● Pausiert", "● En pause", "● En pausa", "● Pausado", "● Приостановлено");
            Add(map, "CatEye Screen Recorder update", "猫眼录屏更新", "CatEye Screen Recorder update", "CatEye Screen Recorder の更新", "CatEye Screen Recorder 업데이트", "CatEye Screen Recorder-Update", "Mise à jour de CatEye Screen Recorder", "Actualización de CatEye Screen Recorder", "Atualização do CatEye Screen Recorder", "Обновление CatEye Screen Recorder");
            Add(map, "Version {0} is available. Download the update?", "發現新版本 {0}，要下載更新嗎？", "Version {0} is available. Download the update?", "新しいバージョン {0} があります。更新をダウンロードしますか？", "새 버전 {0}을(를) 사용할 수 있습니다. 업데이트를 다운로드할까요?", "Version {0} ist verfügbar. Update herunterladen?", "La version {0} est disponible. Télécharger la mise à jour ?", "La versión {0} está disponible. ¿Descargar la actualización?", "A versão {0} está disponível. Baixar a atualização?", "Доступна версия {0}. Скачать обновление?");
            Add(map, "Downloading update…", "正在下載更新…", "Downloading update…", "更新をダウンロード中…", "업데이트 다운로드 중…", "Update wird heruntergeladen…", "Téléchargement de la mise à jour…", "Descargando actualización…", "Baixando atualização…", "Загрузка обновления…");
            Add(map, "Update downloaded and ready to install", "更新已下載，準備安裝", "Update downloaded and ready to install", "更新をダウンロードしました。インストール準備完了", "업데이트 다운로드 완료, 설치 준비됨", "Update geladen und zur Installation bereit", "Mise à jour téléchargée et prête à installer", "Actualización descargada y lista para instalar", "Atualização baixada e pronta para instalar", "Обновление загружено и готово к установке");
            Add(map, "The update is downloaded. Restart now to install it?", "更新包已下載，現在重新啟動安裝嗎？", "The update is downloaded. Restart now to install it?", "更新をダウンロードしました。今すぐ再起動してインストールしますか？", "업데이트가 다운로드되었습니다. 지금 다시 시작하여 설치할까요?", "Das Update ist geladen. Jetzt neu starten und installieren?", "La mise à jour est téléchargée. Redémarrer maintenant pour l’installer ?", "La actualización se descargó. ¿Reiniciar ahora para instalarla?", "A atualização foi baixada. Reiniciar agora para instalar?", "Обновление загружено. Перезапустить для установки?");
            Add(map, "Area selection failed: ", "區域選取失敗：", "Area selection failed: ", "範囲選択に失敗しました: ", "영역 선택 실패: ", "Bereichauswahl fehlgeschlagen: ", "Échec de la sélection : ", "Error al seleccionar el área: ", "Falha ao selecionar a área: ", "Не удалось выбрать область: ");
            Add(map, "This system cannot exclude the floating bar, so the tray controls are used. Right-click the tray icon to pause or stop.", "此系統無法排除懸浮列，已改用系統匣控制。右鍵系統匣圖示可暫停或停止。", "This system cannot exclude the floating bar, so the tray controls are used. Right-click the tray icon to pause or stop.", "このシステムでは操作バーを録画から除外できないため、トレイを使います。トレイアイコンを右クリックして一時停止または停止してください。", "이 시스템은 플로팅 바를 제외할 수 없어 트레이 컨트롤을 사용합니다. 트레이 아이콘을 마우스 오른쪽 버튼으로 클릭하세요.", "Dieses System kann die Leiste nicht ausblenden; verwenden Sie den Tray. Rechtsklick zum Pausieren oder Stoppen.", "Ce système ne peut pas exclure la barre ; utilisez la zone de notification. Clic droit pour pause ou arrêt.", "Este sistema no puede excluir la barra; usa la bandeja. Haz clic derecho para pausar o detener.", "Este sistema não pode excluir a barra; use a bandeja. Clique direito para pausar ou parar.", "Система не может исключить панель; используйте трей. Щёлкните правой кнопкой для паузы или остановки.");
            Add(map, "Saved  ·  ", "已儲存  ·  ", "Saved  ·  ", "保存済み  ·  ", "저장됨  ·  ", "Gespeichert  ·  ", "Enregistré  ·  ", "Guardado  ·  ", "Salvo  ·  ", "Сохранено  ·  ");
            Add(map, "Recording did not finish. Check the error details.", "錄製未完成，請查看錯誤詳情。", "Recording did not finish. Check the error details.", "録画が完了しませんでした。エラーの詳細を確認してください。", "녹화가 완료되지 않았습니다. 오류 세부 정보를 확인하세요.", "Aufnahme nicht abgeschlossen. Prüfen Sie die Fehlerdetails.", "L’enregistrement n’est pas terminé. Consultez les détails de l’erreur.", "La grabación no terminó. Revisa los detalles del error.", "A gravação não terminou. Verifique os detalhes do erro.", "Запись не завершена. Проверьте сведения об ошибке.");
            Add(map, "A recoverable temporary file remains in the recording folder.", "可復原的暫存檔仍保留在錄製資料夾。", "A recoverable temporary file remains in the recording folder.", "復元可能な一時ファイルが録画フォルダーに残っています。", "복구 가능한 임시 파일이 녹화 폴더에 남아 있습니다.", "Eine wiederherstellbare temporäre Datei befindet sich im Aufnahmeordner.", "Un fichier temporaire récupérable reste dans le dossier d’enregistrement.", "Queda un archivo temporal recuperable en la carpeta de grabaciones.", "Um arquivo temporário recuperável permanece na pasta de gravação.", "В папке записи остался временный файл для восстановления.");
            Add(map, "CatEye Screen Recorder · Recording failed", "猫眼录屏 · 錄製失敗", "CatEye Screen Recorder · Recording failed", "CatEye Screen Recorder · 録画失敗", "CatEye Screen Recorder · 녹화 실패", "CatEye Screen Recorder · Aufnahme fehlgeschlagen", "CatEye Screen Recorder · Échec de l’enregistrement", "CatEye Screen Recorder · Error de grabación", "CatEye Screen Recorder · Falha na gravação", "CatEye Screen Recorder · Ошибка записи");
            return map;
        }
        private static void Add(Dictionary<string, string[]> map, string key, params string[] values) { map[key] = values; }
    }

    internal sealed class AboutDialog : Form
    {
        private readonly Label featuresText = new Label();
        internal AboutDialog()
        {
            SuspendLayout(); Text = Localization.ProductTitle; BackColor = Theme.Background; ForeColor = Theme.Text;
            Font = Theme.Font(9, false); AutoScaleDimensions = new SizeF(96, 96); AutoScaleMode = AutoScaleMode.Dpi; FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterParent; ClientSize = new Size(720, 580); MaximizeBox = false; MinimizeBox = false; ShowInTaskbar = false; DoubleBuffered = true;
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
            Panel chrome = new Panel { Bounds = new Rectangle(0, 0, 720, 46), BackColor = Theme.Sidebar }; Controls.Add(chrome);
            Label chromeTitle = Theme.Label(chrome, Localization.ProductTitle, 18, 12, 560, 22, 9, Theme.Muted, false);
            chrome.MouseDown += delegate(object sender, MouseEventArgs e) { if (e.Button == MouseButtons.Left) Native.DragWindow(this); };
            chromeTitle.MouseDown += delegate(object sender, MouseEventArgs e) { if (e.Button == MouseButtons.Left) Native.DragWindow(this); };
            DarkButton closeTop = new DarkButton { Text = "×", Bounds = new Rectangle(676, 7, 36, 32), Danger = true, Chrome = true, TabStop = false, BackColor = Theme.Sidebar }; closeTop.Click += delegate { Close(); }; chrome.Controls.Add(closeTop);
            Label title = Theme.Label(this, Localization.Text("猫眼录屏", "CatEye Screen Recorder"), 28, 66, 664, 44, 18, Theme.Text, true);
            Label subtitle = Theme.Label(this, Localization.Text("无水印、轻量、完全免费的 Windows 录屏工具，打开就能录。", "Free, lightweight and watermark-free. Open and record."), 30, 108, 660, 28, 9, Theme.Accent, false);
            CardPanel card = new CardPanel { Bounds = new Rectangle(28, 146, 664, 354), AutoScroll = true, AutoScrollMinSize = new Size(640, 430) }; Controls.Add(card);
            Panel contentHost = new Panel { Location = new Point(0, 0), Size = new Size(640, 430), BackColor = Theme.Card }; card.Controls.Add(contentHost);
            Section(contentHost, Localization.Text("软件简介", "Software introduction"), new Rectangle(20, 14, 604, 24), 10, Theme.Accent, true);
            Section(contentHost, Localization.Text("猫眼录屏面向 Windows 用户，打开即可录制，轻量、无水印、完全免费。", "A free Windows recorder with no watermark, low resource use and quick setup. Open the app and start."), new Rectangle(20, 42, 604, 42), 9, Theme.Text, false);
            Section(contentHost, Localization.Text("核心特点", "Key features"), new Rectangle(20, 92, 604, 24), 10, Theme.Accent, true);
            featuresText = Section(contentHost, Features(), new Rectangle(20, 120, 604, 96), 9, Theme.Text, false);
            Section(contentHost, Localization.Text("适用场景", "Use cases"), new Rectangle(20, 224, 604, 24), 10, Theme.Accent, true);
            Section(contentHost, Localization.Text("网课、游戏、会议、教程、日常屏幕记录", "Classes, games, meetings, tutorials and daily screen records"), new Rectangle(20, 252, 604, 32), 9, Theme.Text, false);
            Section(contentHost, Localization.Text("收费模式：完全免费\n运行环境：Windows 7 / 10 / 11", "Pricing: Free\nEnvironment: Windows 7 / 10 / 11"), new Rectangle(20, 302, 604, 42), 9, Theme.Muted, false);
            DarkButton close = new DarkButton { Text = Localization.Text("关闭", "Close"), Bounds = new Rectangle(568, 526, 124, 38), Chrome = true, TabStop = false, BackColor = Theme.Accent, ForeColor = Theme.Background }; close.Click += delegate { Close(); }; Controls.Add(close);
            Paint += delegate(object sender, PaintEventArgs e) { using (Pen border = new Pen(Theme.Border)) e.Graphics.DrawRectangle(border, 0, 0, Width - 1, Height - 1); }; ResumeLayout(true);
        }
        private Label Section(Control parent, string text, Rectangle bounds, float size, Color color, bool bold)
        { Label label = new Label { Text = text, Bounds = bounds, Font = Theme.Font(size, bold), ForeColor = color, BackColor = Color.Transparent, AutoSize = false, UseCompatibleTextRendering = true, TextAlign = ContentAlignment.TopLeft }; parent.Controls.Add(label); return label; }
        private static string Features() { return Localization.Text("• 无水印，录制完成即可使用\n• 轻量低占用\n• 完全免费\n• 支持 Windows 7 / 10 / 11", "• No watermark\n• Lightweight\n• Completely free\n• Windows 7 / 10 / 11"); }
    }
}
