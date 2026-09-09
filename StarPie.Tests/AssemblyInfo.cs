using Xunit;

// 测试套件共享语言状态（GeneralSettings / Navigation 等均读写当前语言），
// 并行执行会产生偶发竞争；串行执行保证确定性。
[assembly: CollectionBehavior(DisableTestParallelization = true)]
