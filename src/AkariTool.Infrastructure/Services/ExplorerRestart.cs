namespace AkariTool.Tabs
{
    /// <summary>
    /// Shared Explorer restart with batching. Single-row tweaks call
    /// Request() and get an immediate restart, exactly like the old private
    /// CustomizeTab.RestartExplorer(). Bulk operations wrap their run in
    /// BeginBatch()/EndBatch(): every Request() inside the batch coalesces
    /// into (at most) one restart when the outermost batch ends.
    /// </summary>
    public static class ExplorerRestart
    {
        private static int _batchDepth;
        private static bool _pending;

        /// <summary>Restart Explorer now, or defer to EndBatch inside a batch.</summary>
        public static void Request()
        {
            if (_batchDepth > 0) { _pending = true; return; }
            Restart();
        }

        /// <summary>Starts coalescing Request() calls. Re-entrant.</summary>
        public static void BeginBatch() => _batchDepth++;

        /// <summary>
        /// Ends a batch. When the outermost batch ends with a pending request,
        /// Explorer restarts once. Returns true when a restart happened.
        /// </summary>
        public static bool EndBatch()
        {
            if (_batchDepth > 0) _batchDepth--;
            if (_batchDepth > 0 || !_pending) return false;
            _pending = false;
            Restart();
            return true;
        }

        private static void Restart()
        {
            foreach (var p in System.Diagnostics.Process.GetProcessesByName("explorer"))
                p.Kill();
            System.Diagnostics.Process.Start("explorer.exe");
        }
    }
}
