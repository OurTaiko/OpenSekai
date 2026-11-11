namespace Sekai.MusicScoreMaker.Ingame.Views
{
	/// <summary>The editor option dialog's labels (the original's Japanese strings, now in the ChartEditor table).</summary>
	public static class MusicScoreMakerOptionConstants
	{
		private const string Table = "ChartEditor";

		public static string SECTION_DISPLAY_SETTINGS => L10n.Get(Table, "option.section.display");

		public static string SECTION_OPERATION_SETTINGS => L10n.Get(Table, "option.section.operation");

		public static string SECTION_AUTO_SAVE_SETTINGS => L10n.Get(Table, "option.section.autoSave");

		public static string SECTION_DISPLAY_LINE_SETTINGS => L10n.Get(Table, "option.section.displayLines");

		public static string SECTION_AUDIO_SETTINGS => L10n.Get(Table, "option.section.audio");

		public static string SECTION_CLOCK_MENU_SETTINGS => L10n.Get(Table, "option.section.clockMenu");

		public static string SECTION_SYSTEM => L10n.Get(Table, "option.section.system");

		public static string SETTING_ZOOM_TIMELINE_STEP => L10n.Get(Table, "option.zoomTimelineStep");

		public static string SETTING_ZOOM_TIMELINE_MAX => L10n.Get(Table, "option.zoomTimelineMax");

		public static string SETTING_ZOOM_TIMELINE_MIN => L10n.Get(Table, "option.zoomTimelineMin");

		public static string SETTING_SCORE_DISPLAY_SCALE_H => L10n.Get(Table, "option.scoreDisplayScaleH");

		public static string SETTING_SCORE_DISPLAY_SCALE_V => L10n.Get(Table, "option.scoreDisplayScaleV");

		public static string SETTING_TOOL_WINDOW_CHILD_SCALE => L10n.Get(Table, "option.toolWindowChildScale");

		public static string SETTING_LAYOUT_PATTERN => L10n.Get(Table, "option.layoutPattern");

		public static string SETTING_UNDO_STACK_LIMIT => L10n.Get(Table, "option.undoStackLimit");

		public static string SETTING_SHOW_FOCUS_TICKS_RATE => L10n.Get(Table, "option.showFocusTicksRate");

		public static string SETTING_TICKS_PER_SCROLL_STEP => L10n.Get(Table, "option.ticksPerScrollStep");

		public static string SETTING_ENABLE_SWIPE_SCROLL => L10n.Get(Table, "option.enableSwipeScroll");

		public static string SETTING_AUTO_SAVE_ENABLED => L10n.Get(Table, "option.autoSaveEnabled");

		public static string SETTING_AUTO_SAVE_INTERVAL => L10n.Get(Table, "option.autoSaveInterval");

		public static string SETTING_SHOW_BAR_LINES => L10n.Get(Table, "option.showBarLines");

		public static string SETTING_SHOW_BEAT_LINES => L10n.Get(Table, "option.showBeatLines");

		public static string SETTING_SHOW_QUANTIZE_LINES => L10n.Get(Table, "option.showQuantizeLines");

		public static string SETTING_SHOW_WAVEFORM => L10n.Get(Table, "option.showWaveform");

		public static string SETTING_PLAY_MUSIC_SE_ENABLED => L10n.Get(Table, "option.playMusicSe");

		public static string SETTING_NOTE_EDGE_WIDTH => L10n.Get(Table, "option.noteEdgeWidth");

		public static string SETTING_ENABLE_INVALID_PLACEMENT_CHECK => L10n.Get(Table, "option.invalidPlacementCheck");

		public static string SETTING_AREA_SELECT_PARTIAL_OVERLAP => L10n.Get(Table, "option.areaSelectPartialOverlap");

		public static string SETTING_NOTE_Y_SCALE_START_THRESHOLD => L10n.Get(Table, "option.noteYScaleStartThreshold");

		public static string SETTING_NOTE_Y_SCALE_END_THRESHOLD => L10n.Get(Table, "option.noteYScaleEndThreshold");

		public static string SETTING_NOTE_Y_SCALE_MIN => L10n.Get(Table, "option.noteYScaleMin");

		public static string SETTING_DRAW_SMALLER_TICK_TO_BACK => L10n.Get(Table, "option.drawSmallerTickToBack");

		public static string SETTING_MAX_CLIPBOARD_CACHE_COUNT => L10n.Get(Table, "option.maxClipboardCacheCount");

		public static string SETTING_CLOCK_MENU_ADD_NORMAL_NOTE => L10n.Get(Table, "option.clockMenu.addNormalNote");

		public static string SETTING_CLOCK_MENU_ADD_LONG_NOTE => L10n.Get(Table, "option.clockMenu.addLongNote");

		public static string SETTING_CLOCK_MENU_ADD_FLICK_NOTE => L10n.Get(Table, "option.clockMenu.addFlickNote");

		public static string SETTING_CLOCK_MENU_ADD_FRICTION_NOTE => L10n.Get(Table, "option.clockMenu.addTraceNote");

		public static string SETTING_CLOCK_MENU_ADD_FRICTION_LONG_NOTE => L10n.Get(Table, "option.clockMenu.addTraceLongNote");

		public static string SETTING_CLOCK_MENU_ADD_FRICTION_FLICK_NOTE => L10n.Get(Table, "option.clockMenu.addTraceFlickNote");

		public static string SETTING_CLOCK_MENU_ADD_GUIDE_NOTE => L10n.Get(Table, "option.clockMenu.addGuideNote");

		public static string SETTING_CLOCK_MENU_FLIP_HORIZONTALLY => L10n.Get(Table, "option.clockMenu.flipHorizontally");

		public static string SETTING_CLOCK_MENU_FLIP_VERTICALLY => L10n.Get(Table, "option.clockMenu.flipVertically");

		public static string SETTING_CLOCK_MENU_COPY_NOTES => L10n.Get(Table, "option.clockMenu.copyNotes");

		public static string SETTING_CLOCK_MENU_DELETE_NOTE => L10n.Get(Table, "option.clockMenu.deleteNote");

		public static string SETTING_CLOCK_MENU_EDIT_EVENT_DATA => L10n.Get(Table, "option.clockMenu.editEventData");

		public static string SETTING_CLOCK_MENU_DELETE_EVENT_DATA => L10n.Get(Table, "option.clockMenu.deleteEventData");

		public static string SETTING_CLOCK_MENU_ADD_BPM => L10n.Get(Table, "option.clockMenu.addBpm");

		public static string SETTING_CLOCK_MENU_ADD_TIME_SIGNATURE => L10n.Get(Table, "option.clockMenu.addTimeSignature");

		public static string SETTING_CLOCK_MENU_ADD_HIGH_SPEED => L10n.Get(Table, "option.clockMenu.addHighSpeed");

		public static string SETTING_CLOCK_MENU_PASTE => L10n.Get(Table, "option.clockMenu.paste");

		public static string SECTION_CLOCK_MENU_SECTION_SETTINGS => L10n.Get(Table, "option.section.clockMenuSections");

		public static string SETTING_CLOCK_MENU_SECTION_ADD_NOTE => L10n.Get(Table, "option.clockMenuSection.addNote");

		public static string SETTING_CLOCK_MENU_SECTION_EDIT_NOTE => L10n.Get(Table, "option.clockMenuSection.editNote");

		public static string SETTING_CLOCK_MENU_SECTION_EDIT_EVENT_DATA => L10n.Get(Table, "option.clockMenuSection.editEventData");

		public static string SETTING_CLOCK_MENU_SECTION_ADD_MUSIC_EVENT => L10n.Get(Table, "option.clockMenuSection.addMusicEvent");

		public static string SETTING_CLOCK_MENU_SECTION_PASTE => L10n.Get(Table, "option.clockMenuSection.paste");

		public static string SETTING_RESET_SETTING_DATA => L10n.Get(Table, "option.resetSettingData");

		public static string SETTING_CLEAR_CLIPBOARD_CACHE => L10n.Get(Table, "option.clearClipboardCache");

		public static string SETTING_OPTION_DIALOG => L10n.Get(Table, "option.optionDialog");

		public const string FORMAT_FLOAT_3 = "F3";

		public const string FORMAT_FLOAT_2 = "F2";

		public const string FORMAT_INTEGER = "N0";

		public const string TOGGLE_GROUP_LAYOUT_PATTERN = "layoutPattern";

		public static string PATTERN_NAME_FORMAT => L10n.Get(Table, "option.layoutPatternName");
	}
}
