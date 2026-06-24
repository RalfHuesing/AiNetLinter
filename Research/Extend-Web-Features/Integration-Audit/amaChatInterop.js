// AMA KI-Workspace: Chat-Verlauf nach unten scrollen
window.sanAmaChat = {
  scrollToBottom: function (element) {
    if (!element) return;
    element.scrollTop = element.scrollHeight;
  },
};
