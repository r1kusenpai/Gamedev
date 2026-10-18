#if UNITY_EDITOR
namespace QueenBattle.Editor
{
    // The original three-spell checks are superseded by the expanded reference encounter.
    public static class TestQueenBossBattle
    {
        public static void RunBatch()=>TestQueenReferenceExpansion.Run();
        public static void RunActualHall()=>TestQueenReferenceExpansion.Run();
    }
}
#endif
