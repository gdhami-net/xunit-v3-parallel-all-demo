using Xunit;

namespace Probe;

// Line numbers matter: the check expects a compile error on exactly the DisableParallelism lines.
[TestClass(DisableParallelism = true)] public class ClassWrong { [Fact] public void T() { } }
[TestClass(DisableParallelization = true)] public class ClassRight { [Fact] public void T() { } }
public class FactWrong { [Fact(DisableParallelism = true)] public void T() { } }
public class FactRight { [Fact(DisableParallelization = true)] public void T() { } }
public class TheoryWrong { [Theory(DisableParallelism = true)] [InlineData(1)] public void T(int x) { Assert.Equal(1, x); } }
public class TheoryRight { [Theory(DisableParallelization = true)] [InlineData(1)] public void T(int x) { Assert.Equal(1, x); } }
[CollectionDefinition("a", DisableParallelism = true)] public class CollectionWrong;
[CollectionDefinition("b", DisableParallelization = true)] public class CollectionRight;
public class DataWrong { [Theory] [InlineData(1, DisableParallelism = true)] public void T(int x) { Assert.Equal(1, x); } }
public class DataRight { [Theory] [InlineData(1, DisableParallelization = true)] public void T(int x) { Assert.Equal(1, x); } }
