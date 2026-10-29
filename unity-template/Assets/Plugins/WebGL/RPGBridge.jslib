// 网页版读 RPG 工坊传来的内容：工坊和游戏在同一个网站下，工坊把内容写进 localStorage
mergeInto(LibraryManager.library, {
  RPG_GetPack: function () {
    var s = null;
    try {
      var q = new URLSearchParams(location.search).get('pack') || 'rpgws:playpack';
      s = localStorage.getItem(q);
    } catch (e) {}
    if (!s) return null;
    var n = lengthBytesUTF8(s) + 1;
    var b = _malloc(n);
    stringToUTF8(s, b, n);
    return b;
  }
});
