/// <reference types="@raycast/api">

/* 🚧 🚧 🚧
 * This file is auto-generated from the extension's manifest.
 * Do not modify manually. Instead, update the `package.json` file.
 * 🚧 🚧 🚧 */

/* eslint-disable @typescript-eslint/ban-types */

type ExtensionPreferences = {
  /** Pfad zur PaceAtlas-Anwendung - Optional: Vollständiger Pfad zur PaceAtlas.WinUI.exe. Ohne Angabe wird die installierte Version gesucht. */
  "applicationPath"?: string
}

/** Preferences accessible in all the extension's commands */
declare type Preferences = ExtensionPreferences

declare namespace Preferences {
  /** Preferences accessible in the `status` command */
  export type Status = ExtensionPreferences & {}
  /** Preferences accessible in the `activities` command */
  export type Activities = ExtensionPreferences & {}
  /** Preferences accessible in the `medications` command */
  export type Medications = ExtensionPreferences & {}
  /** Preferences accessible in the `measures` command */
  export type Measures = ExtensionPreferences & {}
}

declare namespace Arguments {
  /** Arguments passed to the `status` command */
  export type Status = {}
  /** Arguments passed to the `activities` command */
  export type Activities = {}
  /** Arguments passed to the `medications` command */
  export type Medications = {}
  /** Arguments passed to the `measures` command */
  export type Measures = {}
}

