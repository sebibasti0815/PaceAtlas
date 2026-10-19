# 0.20.2 – Raycast setup order and repair

On first installation the setup waits for the user to import the extension, then builds it. On upgrades, existing registration is preserved and built. After building, the four entry-point JavaScript files and plugin.json are validated. The Start Menu repair command repeats the build if a later import has removed the files. No unsupported manipulation of Raycast internal files. Windows installer execution has not been verified in this environment.
