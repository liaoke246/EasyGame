mergeInto(LibraryManager.library, {
  EasyGameUpdateSkills: function(cleave, rising, nova, defeated) {
    if (typeof window.easygameSkillState === "function") window.easygameSkillState(cleave, rising, nova, defeated);
  }
});
