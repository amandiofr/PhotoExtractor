record AppConfig(
    string ImagePath,
    int WindowLeft, int WindowTop, int WindowWidth, int WindowHeight,
    int Blur, int Low, int High,
    bool ShowEdges = false, bool ShowLines = false,
    int MinLen = 20, int MaxGap = 15,
    string[] TabPaths = null!, int ActiveTab = 0,
    bool UseLOCR = false,
    bool[] LocrStepChecked = null!,
    float ViewZoom = 1f, float ViewPanX = 0f, float ViewPanY = 0f,
    bool ShowSettings = false,
    int TabScrollOffset = 0
);
