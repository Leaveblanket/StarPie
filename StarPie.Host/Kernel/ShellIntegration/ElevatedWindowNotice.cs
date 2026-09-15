namespace StarPie.Kernel.ShellIntegration
{
    /// <summary>
    /// 高权限窗口手势失效的**一次性**告知决策（ADR-0040 决策 6 / #165）：非提权态下前台出现
    /// 更高完整性级别的窗口时报一次托盘气泡，此后不再报。
    /// </summary>
    /// <remarks>
    /// 与 <c>TrayStateSignal</c>/<c>ShellExitSequence</c> 同属壳层纯决策——输入全由调用方取好，
    /// 本类不碰任何系统调用（前台探测见 <see cref="ProcessElevation.IsForegroundWindowHigherIntegrity"/>，
    /// "已提示过"标记存于 config.json）。三条限定各有理由，缺一条就会误报：
    /// <list type="bullet">
    /// <item><description><b>一次性</b>：提示落在用户正专注别的窗口时，且很多高权限窗口用户根本不在其中用手势；
    /// 高频提示会稀释托盘气泡通道的信噪比。</description></item>
    /// <item><description><b>提权态不报</b>：此时没有可提示的内容（手势在高权限窗口内本就可唤起）。</description></item>
    /// <item><description><b>探测未知不报</b>：未知是"没查成"，不是"低于本进程"；按未知提示就是凭空造噪音。</description></item>
    /// </list>
    /// 提示只能由**前台窗口状态**驱动，不能等手势失败之后再报：低层鼠标钩子收不到指向高权限窗口的输入，
    /// "用户刚划了一下没反应"在进程内不可观测。
    /// </remarks>
    public static class ElevatedWindowNotice
    {
        /// <summary>
        /// 是否还值得继续取样。提权态与"已提示过"在一次进程内是常量，不成立即永远不成立
        /// ——调用方可据此免去一次注定无果的常驻节拍。
        /// </summary>
        /// <param name="isElevated">本进程是否已提权。</param>
        /// <param name="alreadyReported">本安装是否已报过（config.json 的已提示过标记）。</param>
        public static bool ShouldWatch(bool isElevated, bool alreadyReported)
            => !isElevated && !alreadyReported;

        /// <summary>是否该就"前台窗口属于更高完整性级别"报出这一次气泡。</summary>
        /// <param name="isElevated">本进程是否已提权。</param>
        /// <param name="alreadyReported">本安装是否已报过。</param>
        /// <param name="foregroundIsHigherIntegrity">前台窗口完整性级别是否更高；null = 探测未知。</param>
        public static bool ShouldReport(bool isElevated, bool alreadyReported, bool? foregroundIsHigherIntegrity)
            => ShouldWatch(isElevated, alreadyReported) && foregroundIsHigherIntegrity == true;
    }
}
