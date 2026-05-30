// TitleScene relies on MonoGame's static Core singleton (Core.GraphicsDevice,
// Core.Input, Core.ContentManager) which requires a real windowed MonoGame Game
// instance to be initialised before any Scene subclass can be constructed or
// have its methods called.  There is no seam (interface, virtual property, or
// dependency-injection point) that would let us substitute a test double for
// these static members without modifying the production code.
//
// All tests below are therefore marked [Ignore] and document which behaviour
// should be covered once a MonoGame test-host or headless graphics-device
// abstraction is introduced.

namespace PoopMan.UnitTests.Scenes;

[TestClass]
public sealed class TitleSceneTests
{
    [TestMethod]
    [Ignore("ProductionBugSuspected")]
    [TestCategory("ProductionBugSuspected")]
    public void Update_WhenEnterPressedOnItem0_StopsTitleAudioAndChangesToGameScene()
    {
        // _selectedItem == 0, Enter → AudioManager.StopTitleAudio(); Core.ChangeScene(GameScene).
    }

    [TestMethod]
    [Ignore("ProductionBugSuspected")]
    [TestCategory("ProductionBugSuspected")]
    public void Update_WhenEnterPressedOnItem3_SetsScreenToAudio()
    {
        // _selectedItem == 3, Enter → _screen = MenuScreen.Audio.
    }

}
