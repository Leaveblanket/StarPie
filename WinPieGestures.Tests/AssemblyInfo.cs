using Xunit;

// 测试套件共享静态 I18n 语言状态（I18nTests / GeneralSettings / Navigation 等均读写
// I18n.CurrentLanguage），并行执行会产生偶发竞争；串行执行保证确定性。
[assembly: CollectionBehavior(DisableTestParallelization = true)]
