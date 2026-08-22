#!/usr/bin/env python3
"""Generate reviewable exact-schema candidates for built-in automation routes.

The generator deliberately refuses to invent an unrestricted object contract. It extracts
closed top-level shapes from the registered handler source and marks every unresolved value
as ``json`` in its audit report. Maintainers must either improve inference or add an explicit
override before the generated C# catalog can be accepted by the package regression gate.
"""

from __future__ import annotations

import argparse
import copy
import dataclasses
import hashlib
import json
import pathlib
import re
from collections import defaultdict
from typing import Iterable


ROOT = pathlib.Path(__file__).resolve().parents[1]
EDITOR = ROOT / "Editor"
DESCRIPTOR_REGISTRY = EDITOR / "VmAutomationBuiltInRouteDescriptorRegistry.cs"
LOCALIZATION_DESCRIPTOR_PROVIDER = (
    EDITOR / "Localization" / "VmAutomationGeneratedLocalizationRouteProvider.cs")
TOOL_CONFIGURATION_POLICY = EDITOR / "VmAutomationToolConfigurationPolicy.cs"


@dataclasses.dataclass(frozen=True)
class Method:
    class_name: str
    name: str
    return_type: str
    parameters: str
    body: str


@dataclasses.dataclass(frozen=True)
class Field:
    name: str
    kind: str
    required: bool = False
    item_kind: str | None = None
    fields: tuple["Field", ...] = ()
    nullable: bool = False


def exact_object(properties: dict[str, dict[str, object]],
                 required: Iterable[str] = ()) -> dict[str, object]:
    schema: dict[str, object] = {
        "type": "object",
        "properties": properties,
        "additionalProperties": False,
    }
    required_names = list(required)
    if required_names:
        schema["required"] = required_names
    return schema


def exact_array(item: dict[str, object]) -> dict[str, object]:
    return {"type": "array", "items": item}


def one_of(*variants: dict[str, object]) -> dict[str, object]:
    return {"oneOf": list(variants)}


def string_enum(*values: str) -> dict[str, object]:
    return {"type": "string", "enum": list(values)}


NUMBER = {"type": "number"}
INTEGER = {"type": "integer"}
STRING = {"type": "string"}
JSON_VALUE = {"$ref": "#/$defs/unityJsonValue"}


# These fields are structurally validated by their handlers but cannot be recovered from
# the top-level accessor alone. Keep the reviewed shapes beside the generator instead of
# silently degrading them to an untyped array or map.
INPUT_PROPERTY_OVERRIDES: dict[tuple[str, str], dict[str, object]] = {
    ("animation/create-blend-tree", "motions"): exact_array(exact_object({
        "clipPath": STRING,
        "threshold": NUMBER,
        "timeScale": NUMBER,
    })),
    ("animation/set-clip-curve", "keyframes"): exact_array(exact_object({
        "time": NUMBER,
        "value": NUMBER,
    })),
    ("animation/set-object-reference-curve", "keyframes"): exact_array(exact_object({
        "time": NUMBER,
        "assetPath": STRING,
        "assetName": STRING,
        "objectType": STRING,
    }, ("time",))),
    ("asmdef/set-platforms", "excludePlatforms"): exact_array(STRING),
    ("asmdef/set-platforms", "includePlatforms"): exact_array(STRING),
    ("asmdef/update-settings", "versionDefines"): exact_array(exact_object({
        "name": STRING,
        "expression": STRING,
        "define": STRING,
    }, ("name", "expression", "define"))),
    ("gameobject/create", "scale"): {
        "type": "number",
        "description": "Uniform local scale applied to the x, y, and z axes.",
    },
    ("gameobject/set-transform", "scale"): {
        "type": "number",
        "description": "Uniform local scale applied to the x, y, and z axes.",
    },
    ("input/add-composite-binding", "parts"): exact_array(exact_object({
        "name": STRING,
        "path": STRING,
    }, ("name", "path"))),
    ("input/create", "maps"): exact_array(exact_object({
        "name": STRING,
    }, ("name",))),
    ("lighting/create", "shadows"): STRING,
    ("lighting/create-reflection-probe", "size"): exact_object({
        "x": NUMBER, "y": NUMBER, "z": NUMBER,
    }, ("x", "y", "z")),
    ("lod/create", "levels"): {
        "oneOf": [
            INTEGER,
            exact_array(exact_object({"screenRelativeHeight": NUMBER})),
        ],
    },
    ("scenario/create", "virtualEditors"): INTEGER,
    ("particle/create", "startLifetime"): NUMBER,
    ("particle/create", "startSize"): NUMBER,
    ("particle/set-main", "startLifetime"): NUMBER,
    ("particle/set-main", "startSize"): NUMBER,
    ("project-auditor/audit", "categories"): {
        "type": "array", "items": string_enum(
            "Metadata", "AssetIssue", "Shader", "ShaderVariant", "Code",
            "CodeCompilerMessage", "ProjectSetting", "BuildFile", "BuildStep",
            "BuildSummary", "Assembly", "PrecompiledAssembly",
            "ShaderCompilerMessage", "Package", "Texture", "AudioClip",
            "ComputeShaderVariant", "Mesh", "SpriteAtlas", "Material",
            "AnimatorController", "AnimationClip", "Avatar", "AvatarMask",
            "DomainReload", "FirstCustomCategory"),
        "minItems": 1, "uniqueItems": True,
        "description": "Issue categories to analyze. Omit to run every registered Project Auditor module.",
    },
    ("project-auditor/audit", "descriptorIds"): {
        "type": "array", "items": STRING, "minItems": 1,
        "uniqueItems": True,
        "description": "Exact Project Auditor descriptor IDs to return after the audit.",
    },
    ("project-auditor/audit", "severities"): {
        "type": "array", "items": string_enum(
            "Default", "Error", "Critical", "Major", "Moderate", "Minor",
            "Warning", "Info", "None", "Hidden"),
        "minItems": 1, "uniqueItems": True,
        "description": "Exact resolved severities to return after the audit.",
    },
    ("project-auditor/audit", "offset"): {
        "type": "integer", "minimum": 0,
        "description": "Zero-based offset within the deterministically sorted matching issues.",
    },
    ("project-auditor/audit", "limit"): {
        "type": "integer", "minimum": 1, "maximum": 500,
        "description": "Maximum issues returned. Defaults to 100 and is capped at 500.",
    },
    ("sceneview/set-camera", "lookAtSize"): NUMBER,
    ("sceneview/set-camera", "size"): NUMBER,
    ("selection/set", "paths"): exact_array(STRING),
    ("selection/focus-scene-view", "size"): NUMBER,
    ("terrain/add-layer", "tileSizeX"): NUMBER,
    ("terrain/add-layer", "tileSizeY"): NUMBER,
    ("terrain/get-heights-region", "startX"): INTEGER,
    ("terrain/get-heights-region", "startZ"): INTEGER,
    ("terrain/get-heights-region", "step"): INTEGER,
    ("terrain/place-trees", "positions"): exact_array(exact_object({
        "x": NUMBER,
        "z": NUMBER,
    })),
    ("terrain/place-trees", "seed"): INTEGER,
    ("terrain/smooth", "iterations"): INTEGER,
    ("terrain/set-heights-region", "heights"): exact_array(NUMBER),
    ("terrain/set-heights-region", "startX"): INTEGER,
    ("terrain/set-heights-region", "startZ"): INTEGER,
    ("terrain/set-heights-region", "heightSize"): INTEGER,
    ("ui/create-element", "sizeDelta"): exact_object({
        "x": NUMBER, "y": NUMBER,
    }, ("x", "y")),
}

INPUT_REQUIRED_OVERRIDES: dict[str, tuple[str, ...]] = {
    "animation/set-object-reference-curve": ("keyframes",),
    "component/add": ("componentType",),
    "component/get-properties": ("componentType",),
    "component/move": ("componentType",),
    "component/remove": ("componentType",),
    "terrain/export-heightmap": ("path",),
    "terrain/set-heights-region": ("heights",),
}


# FindGameObject accepts the canonical path/instanceId selector pair, but those fields live
# in the shared helper rather than each route handler. Keep the route semantics reviewed
# here so generation publishes the accepted selector without making optional-target routes
# require one. Non-empty tuples are alternative required fields represented by anyOf.
SCENE_GAME_OBJECT_SELECTOR_REQUIREMENTS: dict[str, tuple[str, ...]] = {
    "animation/assign-controller": ("path", "instanceId"),
    "asset/create-prefab": ("gameObjectPath", "path", "instanceId"),
    "audio/create-source": (),
    "component/add": ("path", "instanceId"),
    "component/get-properties": ("path", "instanceId"),
    "component/get-referenceable": ("path", "instanceId"),
    "component/remove": ("path", "instanceId"),
    "component/set-property": ("path", "instanceId"),
    "gameobject/delete": ("path", "instanceId"),
    "gameobject/duplicate": ("path", "instanceId"),
    "gameobject/info": ("path", "instanceId"),
    "gameobject/reparent": ("path", "instanceId"),
    "gameobject/set-active": ("path", "instanceId"),
    "gameobject/set-transform": ("path", "instanceId"),
    "prefab/apply-overrides": ("path", "instanceId"),
    "prefab/info": ("assetPath", "path", "instanceId"),
    "prefab/revert-overrides": ("path", "instanceId"),
    "prefab/unpack": ("path", "instanceId"),
    "renderer/set-material": ("path", "instanceId"),
    "selection/focus-scene-view": (),
    "taglayer/set-layer": ("path", "instanceId"),
    "taglayer/set-static": ("path", "instanceId"),
    "taglayer/set-tag": ("path", "instanceId"),
}


BOOLEAN = {"type": "boolean"}
NULL = {"type": "null"}
NULLABLE_NUMBER = {"type": ["number", "null"]}
NULLABLE_INTEGER = {"type": ["integer", "null"]}
NULLABLE_BOOLEAN = {"type": ["boolean", "null"]}
NULLABLE_STRING = {"type": ["string", "null"]}
STRING_ARRAY = exact_array(STRING)
JSON_ARRAY = exact_array(JSON_VALUE)
JSON_MAP = {"type": "object", "additionalProperties": JSON_VALUE}
VECTOR2 = exact_object({"x": NUMBER, "y": NUMBER}, ("x", "y"))
VECTOR3 = exact_object({"x": NUMBER, "y": NUMBER, "z": NUMBER}, ("x", "y", "z"))
PLAY_MODE_OPTIONS_STATE = exact_object({
    "enabled": BOOLEAN,
    "optionsValue": INTEGER,
    "disableDomainReload": BOOLEAN,
    "disableSceneReload": BOOLEAN,
    "domainReloadEnabled": BOOLEAN,
    "sceneReloadEnabled": BOOLEAN,
}, ("enabled", "optionsValue", "disableDomainReload", "disableSceneReload",
    "domainReloadEnabled", "sceneReloadEnabled"))
RECT = exact_object({
    "x": NUMBER, "y": NUMBER, "width": NUMBER, "height": NUMBER,
}, ("x", "y", "width", "height"))
UI_RECT = exact_object({
    "x": NULLABLE_NUMBER, "y": NULLABLE_NUMBER,
    "width": NULLABLE_NUMBER, "height": NULLABLE_NUMBER,
    "xMin": NULLABLE_NUMBER, "yMin": NULLABLE_NUMBER,
    "xMax": NULLABLE_NUMBER, "yMax": NULLABLE_NUMBER,
}, ("x", "y", "width", "height", "xMin", "yMin", "xMax", "yMax"))
NULLABLE_UI_RECT = one_of(NULL, UI_RECT)
ASSET_REFERENCE = exact_object({
    "path": STRING, "direction": STRING, "guid": STRING, "type": STRING,
}, ("path", "direction", "guid", "type"))
ASSET_LIST_ITEM = exact_object({
    "path": STRING, "name": STRING, "type": STRING, "guid": STRING,
    "isFolder": BOOLEAN,
}, ("path", "name", "type", "guid", "isFolder"))
ADDRESSABLE_ENTRY = exact_object({
    "guid": STRING, "assetPath": STRING, "address": STRING,
    "labels": STRING_ARRAY, "group": STRING,
}, ("guid", "assetPath", "address", "labels", "group"))
ADDRESSABLE_OPERATION_RESULT = exact_object({
    "action": STRING, "group": STRING, "schemaTypes": STRING_ARRAY,
    "removed": BOOLEAN, "label": STRING, "guid": STRING,
    "entry": {"oneOf": [ADDRESSABLE_ENTRY, {"type": "null"}]},
    "setAsDefault": BOOLEAN, "copySchemas": BOOLEAN, "copySchemasFrom": STRING,
    "entryCount": INTEGER, "oldLabel": STRING, "newLabel": STRING,
    "assetPath": STRING, "existing": BOOLEAN, "address": STRING,
    "enabled": BOOLEAN,
}, ("action",))
ANIMATION_ISSUE = exact_object({
    "type": STRING, "target": STRING, "message": STRING,
}, ("type", "target", "message"))
CONSOLE_ENTRY = exact_object({
    "message": STRING, "type": STRING, "timestamp": STRING,
    "source": STRING, "stackTrace": STRING,
}, ("message", "type", "timestamp", "source"))
PHYSICS_COLLIDER = exact_object({
    "gameObject": STRING, "colliderType": STRING, "instanceId": STRING,
    "position": VECTOR3,
}, ("gameObject", "colliderType", "instanceId"))
PHYSICS_HIT = exact_object({
    "gameObject": STRING, "instanceId": STRING, "point": VECTOR3,
    "normal": VECTOR3, "distance": NUMBER, "colliderType": STRING,
}, ("gameObject", "instanceId", "point", "normal", "distance", "colliderType"))
SPRITE_INFO = exact_object({
    "name": STRING, "rect": RECT, "pivot": VECTOR2, "pixelsPerUnit": NUMBER,
}, ("name", "rect", "pivot", "pixelsPerUnit"))
TEXTCORE_SPRITE_UPSERT_RESULT = exact_object({
    "name": STRING, "imagePath": STRING, "created": BOOLEAN,
    "glyphIndex": INTEGER, "spriteLocalId": INTEGER,
    "rect": exact_object({
        "x": INTEGER, "y": INTEGER, "width": INTEGER, "height": INTEGER,
    }, ("x", "y", "width", "height")),
    "metrics": exact_object({
        "width": NUMBER, "height": NUMBER, "bearingX": NUMBER,
        "bearingY": NUMBER, "advance": NUMBER,
    }, ("width", "height", "bearingX", "bearingY", "advance")),
    "glyphScale": NUMBER, "characterScale": NUMBER,
}, ("name", "imagePath", "created", "glyphIndex", "spriteLocalId", "rect",
    "metrics", "glyphScale", "characterScale"))
SHADER_EDGE = exact_object({
    "outputNodeId": STRING, "outputSlotId": INTEGER,
    "inputNodeId": STRING, "inputSlotId": INTEGER,
}, ("outputNodeId", "outputSlotId", "inputNodeId", "inputSlotId"))
LOCALIZATION_ISSUE = exact_object({
    "collection": STRING, "type": STRING, "key": STRING, "keyId": STRING,
    "locale": STRING, "issue": STRING,
}, ("collection", "type", "key", "keyId", "locale", "issue"))
LOCALIZATION_TABLE = exact_object({
    "locale": STRING, "assetPath": STRING,
    "entryCount": INTEGER, "preload": BOOLEAN,
}, ("locale", "assetPath", "entryCount", "preload"))
LOCALIZATION_COLLECTION = exact_object({
    "name": STRING, "guid": STRING, "type": STRING, "group": STRING,
    "assetPath": STRING, "sharedDataPath": STRING,
    "keyCount": INTEGER, "tableCount": INTEGER,
    "tables": exact_array(LOCALIZATION_TABLE),
}, ("name", "guid", "type", "group", "assetPath", "sharedDataPath",
    "keyCount", "tableCount", "tables"))
PACKAGE_LIST_ITEM = exact_object({
    "name": STRING, "displayName": STRING, "version": STRING,
    "source": STRING, "description": STRING,
}, ("name", "displayName", "version", "source", "description"))
PACKAGE_SEARCH_ITEM = exact_object({
    "name": STRING, "displayName": STRING, "version": STRING,
    "description": STRING,
}, ("name", "displayName", "version", "description"))
EXECUTION_RESULT = exact_object({
    "requestedMode": STRING, "resolvedMode": STRING,
    "operationCount": INTEGER, "operationsPerFrame": INTEGER,
    "frameBudgetMs": INTEGER, "timeoutMs": INTEGER,
    "continueOnError": BOOLEAN,
}, ("requestedMode", "resolvedMode", "operationCount", "operationsPerFrame",
    "frameBudgetMs", "timeoutMs", "continueOnError"))
TEXTURE_IMPORTER_RESULT = exact_object({
    "type": STRING, "spriteMode": STRING, "pixelsPerUnit": NUMBER,
    "filterMode": STRING, "isReadable": BOOLEAN, "compression": STRING,
    "alphaIsTransparency": BOOLEAN, "meshType": STRING,
    "mipmapEnabled": BOOLEAN,
}, ("type", "spriteMode", "pixelsPerUnit", "filterMode", "isReadable",
    "compression", "alphaIsTransparency", "meshType", "mipmapEnabled"))
SPRITE_SLICE_RESULT = exact_object({
    "success": BOOLEAN, "texturePath": STRING,
    "textureWidth": INTEGER, "textureHeight": INTEGER,
    "frameWidth": INTEGER, "frameHeight": INTEGER,
    "spriteCount": INTEGER, "sprites": exact_array(SPRITE_INFO),
}, ("success", "texturePath", "textureWidth", "textureHeight", "frameWidth",
    "frameHeight", "spriteCount", "sprites"))
SUB_ASSET_RESULT = exact_object({
    "name": STRING, "type": STRING, "guid": STRING, "fileID": INTEGER,
}, ("name", "type", "guid", "fileID"))
BATCH_IMPORT_RESULT = exact_object({
    "index": INTEGER, "sourcePath": STRING, "destinationPath": STRING,
    "overwrite": BOOLEAN, "existedBefore": BOOLEAN, "existsNow": BOOLEAN,
    "originalGuid": STRING, "currentGuid": STRING, "imported": BOOLEAN,
    "skipped": BOOLEAN, "duplicate": BOOLEAN, "dedupeMode": STRING,
    "dedupeScope": STRING, "dedupeSearchPath": STRING, "onDuplicate": STRING,
    "contentHash": STRING, "imageWidth": INTEGER, "imageHeight": INTEGER,
    "duplicateAssetPath": STRING, "duplicateAssetGuid": STRING,
    "duplicateSourceIndex": INTEGER, "duplicateSourcePath": STRING,
    "rolledBack": BOOLEAN, "error": STRING, "rollbackError": STRING,
    "importer": one_of(NULL, TEXTURE_IMPORTER_RESULT),
    "spriteSlice": one_of(NULL, SPRITE_SLICE_RESULT),
    "subAssets": exact_array(SUB_ASSET_RESULT),
}, ("index", "sourcePath", "destinationPath", "overwrite", "existedBefore",
    "existsNow", "originalGuid", "currentGuid", "imported", "skipped",
    "duplicate", "dedupeMode", "dedupeScope", "dedupeSearchPath",
    "onDuplicate", "contentHash", "imageWidth", "imageHeight",
    "duplicateAssetPath", "duplicateAssetGuid", "duplicateSourceIndex",
    "duplicateSourcePath", "rolledBack", "error", "rollbackError", "importer",
    "spriteSlice", "subAssets"))
BATCH_MOVE_RESULT = exact_object({
    "index": INTEGER, "oldPath": STRING, "requestedDestinationPath": STRING,
    "targetPath": STRING, "currentPath": STRING, "oldGuid": STRING,
    "currentGuid": STRING, "guidChanged": BOOLEAN, "metaPreserved": BOOLEAN,
    "oldMetaPath": STRING, "currentMetaPath": STRING,
    "oldMetaExists": BOOLEAN, "currentMetaExists": BOOLEAN, "moved": BOOLEAN,
    "rolledBack": BOOLEAN, "spriteNameSynchronizationAttempted": BOOLEAN,
    "synchronizedSpriteNames": BOOLEAN, "synchronizedSpriteCount": INTEGER,
    "spriteImportMode": STRING, "spriteNameSynchronizationError": STRING,
    "singleSpriteNameSynchronizationAttempted": BOOLEAN,
    "synchronizedSingleSpriteName": BOOLEAN,
    "synchronizedMultipleSpriteNames": BOOLEAN, "error": STRING,
}, ("index", "oldPath", "requestedDestinationPath", "targetPath", "currentPath",
    "oldGuid", "currentGuid", "guidChanged", "metaPreserved", "oldMetaPath",
    "currentMetaPath", "oldMetaExists", "currentMetaExists", "moved",
    "rolledBack", "spriteNameSynchronizationAttempted", "synchronizedSpriteNames",
    "synchronizedSpriteCount", "spriteImportMode",
    "spriteNameSynchronizationError", "singleSpriteNameSynchronizationAttempted",
    "synchronizedSingleSpriteName", "synchronizedMultipleSpriteNames", "error"))
SCRIPTABLE_OBJECT_TYPE = exact_object({
    "name": STRING, "fullName": STRING, "assembly": STRING,
}, ("name", "fullName", "assembly"))
LOCALIZATION_UPSERT_ENTRY = exact_object({
    "index": INTEGER, "key": STRING, "locale": STRING,
    "createdKey": BOOLEAN, "createdEntry": BOOLEAN,
}, ("index", "key", "locale", "createdKey", "createdEntry"))
RUNTIME_UI_DOCUMENT = exact_object({
    "instanceId": STRING, "name": STRING, "enabled": BOOLEAN,
    "gameObjectName": STRING, "gameObjectPath": STRING,
    "gameObjectActive": BOOLEAN, "visualTreeAsset": STRING,
    "visualTreeAssetPath": STRING, "panelSettings": STRING,
    "panelSettingsPath": STRING, "hasRootVisualElement": BOOLEAN,
    "rootChildCount": INTEGER, "rootWorldBound": NULLABLE_UI_RECT,
}, ("instanceId", "name", "enabled", "gameObjectName", "gameObjectPath",
    "gameObjectActive", "visualTreeAsset", "visualTreeAssetPath", "panelSettings",
    "panelSettingsPath", "hasRootVisualElement", "rootChildCount", "rootWorldBound"))
SERIALIZED_PROPERTY_INFO = exact_object({
    "name": STRING, "displayName": STRING, "propertyPath": STRING,
    "type": STRING, "editable": BOOLEAN, "isArray": BOOLEAN,
    "arraySize": INTEGER, "value": JSON_VALUE,
}, ("name", "displayName", "propertyPath", "type", "editable", "isArray",
    "arraySize", "value"))
WINDOW_INFO = exact_object({
    "instanceId": STRING, "title": STRING, "type": STRING,
    "fullType": STRING, "hasRootVisualElement": BOOLEAN,
    "rootChildCount": INTEGER,
}, ("instanceId", "title", "type", "fullType", "hasRootVisualElement",
    "rootChildCount"))
UI_CONTEXT = one_of(RUNTIME_UI_DOCUMENT, WINDOW_INFO)
SIZE2 = exact_object({"width": NUMBER, "height": NUMBER}, ("width", "height"))
UI_BUILDER_PREVIEW = exact_object({
    "ready": BOOLEAN,
    "documentPathMatches": BOOLEAN,
    "activeUxmlPath": STRING,
    "documentRootChildCount": INTEGER,
    "canvasChildCount": INTEGER,
    "documentRootSize": SIZE2,
    "canvasSize": SIZE2,
    "configuredCanvasSize": SIZE2,
    "requiredCanvasSize": SIZE2,
    "matchGameView": NULLABLE_BOOLEAN,
    "contentElementCount": INTEGER,
    "contentFitsCanvas": BOOLEAN,
    "canvasTooSmall": BOOLEAN,
    "contentOverflow": exact_object({
        "left": NUMBER, "top": NUMBER, "right": NUMBER, "bottom": NUMBER,
    }, ("left", "top", "right", "bottom")),
    "documentRootWorldBound": UI_RECT,
    "canvasWorldBound": UI_RECT,
    "viewportWorldBound": UI_RECT,
    "contentWorldBound": UI_RECT,
    "error": STRING,
}, ("ready", "documentPathMatches", "activeUxmlPath",
    "documentRootChildCount", "canvasChildCount", "documentRootSize",
    "canvasSize", "configuredCanvasSize", "requiredCanvasSize",
    "matchGameView", "contentElementCount", "contentFitsCanvas",
    "canvasTooSmall", "contentOverflow", "documentRootWorldBound",
    "canvasWorldBound", "viewportWorldBound", "contentWorldBound", "error"))
UI_BUILDER_CANVAS_ADJUSTMENT = exact_object({
    "autoMatchGameView": BOOLEAN,
    "requireContentFit": BOOLEAN,
    "attempted": BOOLEAN,
    "applied": BOOLEAN,
    "attemptedAtFrame": INTEGER,
    "initialMatchGameView": NULLABLE_BOOLEAN,
    "finalMatchGameView": NULLABLE_BOOLEAN,
    "initialCanvasSize": SIZE2,
    "initialRequiredCanvasSize": SIZE2,
    "finalCanvasSize": SIZE2,
    "finalRequiredCanvasSize": SIZE2,
    "contentFitsCanvas": BOOLEAN,
    "error": STRING,
}, ("autoMatchGameView", "requireContentFit", "attempted", "applied",
    "attemptedAtFrame", "initialMatchGameView", "finalMatchGameView",
    "initialCanvasSize", "initialRequiredCanvasSize", "finalCanvasSize",
    "finalRequiredCanvasSize", "contentFitsCanvas", "error"))
UI_BUILDER_VISUAL_ANALYSIS = exact_object({
    "visualValid": BOOLEAN,
    "documentVisuallyBlank": BOOLEAN,
    "conclusive": BOOLEAN,
    "reason": STRING,
    "error": STRING,
    "documentRect": RECT,
    "sampledDocumentRect": RECT,
    "canvasRect": RECT,
    "sampleStep": INTEGER,
    "documentSamples": INTEGER,
    "backgroundSamples": INTEGER,
    "documentDistinctColorBuckets": INTEGER,
    "backgroundDistinctColorBuckets": INTEGER,
    "backgroundPaletteBucketCount": INTEGER,
    "backgroundComparable": BOOLEAN,
    "backgroundOverlapSamples": INTEGER,
    "backgroundOverlapRatio": NUMBER,
    "outOfBackgroundPaletteSamples": INTEGER,
    "minimumOutOfPaletteSamples": INTEGER,
    "outOfBackgroundPaletteRatio": NUMBER,
    "histogramDistance": NUMBER,
    "hasOutOfPaletteEvidence": BOOLEAN,
    "hasDistributionEvidence": BOOLEAN,
    "neutralDocumentSamples": INTEGER,
    "neutralDocumentRatio": NUMBER,
    "dominantDocumentBucketRatio": NUMBER,
    "hasTargetColorEvidence": BOOLEAN,
    "hasTargetComplexityEvidence": BOOLEAN,
    "mappingMode": STRING,
    "contentRect": RECT,
    "documentWorldBound": RECT,
    "canvasWorldBound": RECT,
    "viewportWorldBound": RECT,
}, ("visualValid", "documentVisuallyBlank", "conclusive", "reason"))

STRING_MAP = {"type": "object", "additionalProperties": STRING}
VECTOR4 = exact_object({
    "x": NUMBER, "y": NUMBER, "z": NUMBER, "w": NUMBER,
}, ("x", "y", "z", "w"))
BUILD_SCENE = exact_object({
    "path": STRING, "enabled": BOOLEAN, "guid": STRING,
}, ("path", "enabled", "guid"))
BUILD_PROFILE = exact_object({
    "assetPath": STRING, "name": STRING, "active": BOOLEAN,
    "buildTarget": STRING, "subtarget": STRING, "platformId": STRING,
    "overrideGlobalScenes": BOOLEAN, "hasScriptingDefines": BOOLEAN,
    "scriptingDefines": STRING_ARRAY, "scenes": exact_array(BUILD_SCENE),
    "canBuildLocally": BOOLEAN,
}, ("assetPath", "name", "active", "buildTarget", "subtarget", "platformId",
    "overrideGlobalScenes", "hasScriptingDefines", "scriptingDefines", "scenes",
    "canBuildLocally"))
ACTIVE_BUILD_PROFILE = exact_object({
    "name": STRING, "assetPath": STRING,
}, ("name", "assetPath"))
BUILD_PROFILE_INFO = exact_object({
    "available": BOOLEAN,
    "activeProfile": one_of(NULL, ACTIVE_BUILD_PROFILE),
    "profileCount": INTEGER, "offset": INTEGER, "limit": INTEGER,
    "profiles": exact_array(BUILD_PROFILE), "hasMore": BOOLEAN,
    "nextOffset": NULLABLE_INTEGER, "globalScenes": exact_array(BUILD_SCENE),
}, ("available", "activeProfile", "profileCount", "offset", "limit",
    "profiles", "hasMore", "nextOffset", "globalScenes"))
BUILD_PROFILE_OPERATION = exact_object({
    "action": STRING, "assetPath": STRING, "profileName": STRING,
    "overrideGlobalScenes": BOOLEAN, "scenes": exact_array(BUILD_SCENE),
    "defines": STRING_ARRAY, "propertyPath": STRING,
    "before": JSON_VALUE, "requested": JSON_VALUE,
    "sceneCount": INTEGER, "profile": BUILD_PROFILE,
}, ("action",))
MATERIAL_INFO = exact_object({
    "name": STRING, "shader": STRING, "renderQueue": INTEGER,
    "enableInstancing": BOOLEAN, "doubleSidedGI": BOOLEAN,
    "globalIlluminationFlags": STRING, "keywords": STRING_ARRAY,
    "properties": JSON_MAP,
}, ("name", "shader", "renderQueue", "enableInstancing", "doubleSidedGI",
    "globalIlluminationFlags", "keywords", "properties"))
RENDERER_MATERIAL = exact_object({
    "name": STRING, "shaderName": STRING, "assetPath": STRING,
    "renderQueue": INTEGER,
}, ("name",))
PACKAGE_META_MISSING = exact_object({
    "path": STRING, "expectedMeta": STRING, "type": STRING,
}, ("path", "expectedMeta", "type"))
PACKAGE_META_RESULT = exact_object({
    "name": STRING, "path": STRING, "error": STRING,
    "isValid": BOOLEAN, "missingCount": INTEGER,
    "returnedMissingCount": INTEGER, "truncated": BOOLEAN,
    "missing": exact_array(PACKAGE_META_MISSING),
}, ("name", "path"))
PARTICLE_MAIN = exact_object({
    "duration": NUMBER, "loop": BOOLEAN, "startLifetime": NUMBER,
    "startSpeed": NUMBER, "startSize": NUMBER, "startRotation": NUMBER,
    "maxParticles": INTEGER, "gravityModifier": NUMBER,
    "simulationSpace": STRING, "playOnAwake": BOOLEAN, "scalingMode": STRING,
}, ("duration", "loop", "startLifetime", "startSpeed", "startSize",
    "startRotation", "maxParticles", "gravityModifier", "simulationSpace",
    "playOnAwake", "scalingMode"))
PREFAB_HIERARCHY_NODE = exact_object({
    "name": STRING, "active": BOOLEAN, "tag": STRING, "layer": STRING,
    "components": STRING_ARRAY, "localPosition": VECTOR3,
    "localRotation": JSON_VALUE, "localScale": VECTOR3,
    "children": JSON_ARRAY, "childCount": INTEGER,
    "childrenIncluded": INTEGER, "childrenTruncated": BOOLEAN,
}, ("name", "active", "tag", "layer", "components"))
MISSING_REFERENCE = exact_object({
    "gameObject": STRING, "path": STRING, "issue": STRING,
    "componentIndex": INTEGER, "component": STRING, "property": STRING,
}, ("gameObject", "path", "issue"))
SPRITE_PIXEL_INFO = exact_object({
    "name": STRING, "rect": RECT, "pivotPixels": VECTOR2,
    "pivotNormalized": VECTOR2, "border": VECTOR4,
    "pixelsPerUnit": NUMBER, "warnings": STRING_ARRAY, "valid": BOOLEAN,
}, ("name", "rect", "pivotPixels", "pivotNormalized", "border",
    "pixelsPerUnit", "warnings", "valid"))
SPRITE_PIXEL_RESULT = exact_object({
    "assetPath": STRING, "valid": BOOLEAN, "error": STRING,
    "textureName": STRING, "textureWidth": INTEGER, "textureHeight": INTEGER,
    "textureType": STRING, "spriteImportMode": STRING, "filterMode": STRING,
    "mipmapEnabled": BOOLEAN, "alphaIsTransparency": BOOLEAN,
    "spritePixelsPerUnit": NUMBER, "textureCompression": STRING,
    "defaultPlatformFormat": STRING, "defaultPlatformCompression": STRING,
    "defaultPlatformMaxTextureSize": INTEGER, "warnings": STRING_ARRAY,
    "spriteCount": INTEGER, "sprites": exact_array(SPRITE_PIXEL_INFO),
}, ("assetPath", "valid"))
ANIMATION_CLIP_UPDATE = exact_object({
    "clipPath": STRING, "texturePath": STRING, "bindingPath": STRING,
    "frameRate": NUMBER, "keyframeCount": INTEGER, "sprites": STRING_ARRAY,
}, ("clipPath", "texturePath", "bindingPath", "frameRate", "keyframeCount",
    "sprites"))
TEXTURE_COMPARISON = exact_object({
    "name": STRING, "matches": BOOLEAN,
    "expected": JSON_VALUE, "actual": JSON_VALUE,
}, ("name", "matches", "expected", "actual"))
TEXTURE_IMPORT_CHECK_RESULT = exact_object({
    "assetPath": STRING, "valid": BOOLEAN, "error": STRING,
    "mismatchCount": INTEGER, "comparisonCount": INTEGER,
    "textureWidth": INTEGER, "textureHeight": INTEGER,
    "textureType": STRING, "spriteImportMode": STRING, "filterMode": STRING,
    "mipmapEnabled": BOOLEAN, "textureCompression": STRING,
    "alphaIsTransparency": BOOLEAN, "spritePixelsPerUnit": NUMBER,
    "defaultPlatformFormat": STRING, "defaultPlatformCompression": STRING,
    "comparisons": exact_array(TEXTURE_COMPARISON),
}, ("assetPath", "valid"))
SPRITE_ATLAS_PACKING_SETTINGS = exact_object({
    "blockOffset": INTEGER, "padding": INTEGER,
    "enableRotation": BOOLEAN, "enableTightPacking": BOOLEAN,
    "enableAlphaDilation": BOOLEAN,
}, ("blockOffset", "padding", "enableRotation", "enableTightPacking",
    "enableAlphaDilation"))
UXML_ELEMENT = exact_object({
    "path": STRING, "type": STRING, "fullType": STRING, "name": STRING,
    "classes": STRING_ARRAY, "inlineStyle": STRING, "line": INTEGER,
    "ussMatchedClasses": STRING_ARRAY, "ussDefaultSize": STRING_MAP,
    "ussResolvedDeclarations": STRING_MAP,
}, ("path", "type", "fullType", "name", "classes", "inlineStyle", "line",
    "ussMatchedClasses", "ussDefaultSize", "ussResolvedDeclarations"))
UXML_NAME_CHECK = exact_object({
    "name": STRING, "exists": BOOLEAN, "matchCount": INTEGER,
    "typeMatches": BOOLEAN, "reportedMatchCount": INTEGER,
    "matchesTruncated": BOOLEAN, "matches": exact_array(UXML_ELEMENT),
}, ("name", "exists", "matchCount", "typeMatches", "reportedMatchCount",
    "matchesTruncated", "matches"))
USS_SELECTOR_RULE = exact_object({
    "selector": STRING, "sourcePath": STRING, "pseudoStates": STRING_ARRAY,
    "declarations": STRING_MAP, "defaultSize": STRING_MAP,
}, ("selector", "sourcePath", "pseudoStates", "declarations", "defaultSize"))
USS_CLASS_STYLE = exact_object({
    "className": STRING, "sourcePaths": STRING_ARRAY,
    "defaultSelectors": STRING_ARRAY, "declarations": STRING_MAP,
    "defaultSize": STRING_MAP,
    "contextRules": exact_array(USS_SELECTOR_RULE),
    "stateRules": exact_array(USS_SELECTOR_RULE),
}, ("className", "sourcePaths", "defaultSelectors", "declarations",
    "defaultSize", "contextRules", "stateRules"))
UI_INLINE_STYLE = exact_object({name: STRING for name in (
    "display", "visibility", "position", "left", "top", "right", "bottom",
    "width", "height", "minWidth", "minHeight", "maxWidth", "maxHeight",
    "flexGrow", "flexShrink", "flexBasis", "flexDirection", "alignItems",
    "alignSelf", "justifyContent", "marginLeft", "marginTop", "marginRight",
    "marginBottom", "paddingLeft", "paddingTop", "paddingRight", "paddingBottom",
    "backgroundColor", "unityBackgroundImageTintColor", "color", "opacity",
)})
UI_RESOLVED_STYLE = exact_object({
    **{name: STRING for name in (
        "display", "visibility", "position", "minWidth", "minHeight", "maxWidth",
        "maxHeight", "flexBasis", "flexDirection", "alignItems", "alignSelf",
        "justifyContent", "backgroundColor", "unityBackgroundImageTintColor", "color",
    )},
    **{name: NUMBER for name in (
        "left", "top", "right", "bottom", "width", "height", "flexGrow",
        "flexShrink", "marginLeft", "marginTop", "marginRight", "marginBottom",
        "paddingLeft", "paddingTop", "paddingRight", "paddingBottom", "opacity",
    )},
})
UI_BACKGROUND_REFERENCE = exact_object({
    "name": STRING, "type": STRING, "instanceId": STRING, "assetPath": STRING,
}, ("name", "type", "instanceId", "assetPath"))
UI_BACKGROUND_VALUE = exact_object({
    "text": STRING, "texture": UI_BACKGROUND_REFERENCE,
    "sprite": UI_BACKGROUND_REFERENCE, "renderTexture": UI_BACKGROUND_REFERENCE,
    "vectorImage": UI_BACKGROUND_REFERENCE,
}, ("text",))
UI_BACKGROUND = exact_object({
    "inline": UI_BACKGROUND_VALUE, "resolved": UI_BACKGROUND_VALUE,
}, ("inline", "resolved"))
UI_ELEMENT_INFO = exact_object({
    "path": STRING, "name": STRING, "type": STRING, "fullType": STRING,
    "classes": STRING_ARRAY, "text": STRING, "tooltip": STRING,
    "visible": BOOLEAN, "enabledSelf": BOOLEAN, "enabledInHierarchy": BOOLEAN,
    "pickingMode": STRING, "childCount": INTEGER, "layout": RECT,
    "worldBound": RECT, "inlineStyle": UI_INLINE_STYLE,
    "resolvedStyle": UI_RESOLVED_STYLE, "background": UI_BACKGROUND,
    "children": JSON_ARRAY,
}, ("path", "name", "type", "fullType", "classes", "text", "tooltip",
    "visible", "enabledSelf", "enabledInHierarchy", "pickingMode", "childCount",
    "layout", "worldBound"))
UI_CAPTURE_RESULT = exact_object({
    "runtime": BOOLEAN, "context": UI_CONTEXT, "element": UI_ELEMENT_INFO,
    "pixelScale": NUMBER, "padding": INTEGER, "cropMode": STRING,
    "cropRect": RECT, "windowCapture": JSON_VALUE, "elementCapture": JSON_VALUE,
    "error": STRING, "warning": STRING,
}, ("runtime", "context", "element", "pixelScale", "padding", "cropMode",
    "cropRect", "windowCapture", "elementCapture", "error", "warning"))
IMAGE_SIZE = exact_object({
    "width": INTEGER, "height": INTEGER,
}, ("width", "height"))
IMAGE_COLOR = exact_object({
    "r": NUMBER, "g": NUMBER, "b": NUMBER, "a": NUMBER,
}, ("r", "g", "b", "a"))
IMAGE_COMPARISON_SAMPLE = exact_object({
    "x": INTEGER, "y": INTEGER, "delta": INTEGER,
    "expected": IMAGE_COLOR, "actual": IMAGE_COLOR,
}, ("x", "y", "delta", "expected", "actual"))
IMAGE_COMPARISON = exact_object({
    "expectedPath": STRING, "actualPath": STRING,
    "expectedSize": IMAGE_SIZE, "actualSize": IMAGE_SIZE,
    "comparedWidth": INTEGER, "comparedHeight": INTEGER,
    "tolerance": INTEGER, "differentPixels": INTEGER, "totalPixels": INTEGER,
    "differentRatio": NUMBER, "averageDelta": NUMBER, "maxDelta": INTEGER,
    "samples": exact_array(IMAGE_COMPARISON_SAMPLE), "differenceBounds": RECT,
    "diffOutputPath": STRING, "absoluteDiffOutputPath": STRING,
}, ("expectedPath", "actualPath", "expectedSize", "actualSize", "comparedWidth",
    "comparedHeight", "tolerance", "differentPixels", "totalPixels",
    "differentRatio", "averageDelta", "maxDelta", "samples"))
LOCALIZATION_VARIABLE = exact_object({
    "name": STRING, "type": STRING,
    "value": one_of(BOOLEAN, INTEGER, NUMBER, STRING), "assetPath": STRING,
}, ("name", "type", "value", "assetPath"))
LOCALIZATION_VARIABLE_GROUP = exact_object({
    "name": STRING, "assetPath": STRING,
    "variables": exact_array(LOCALIZATION_VARIABLE),
}, ("name", "assetPath", "variables"))
UXML_QUERY = exact_object({
    "name": STRING, "names": STRING_ARRAY, "className": STRING, "typeName": STRING,
}, ("name", "names", "className", "typeName"))
TEST_FAILURE = exact_object({
    "name": STRING, "fullName": STRING, "message": STRING,
}, ("name", "fullName", "message"))
TEST_PROGRESS = exact_object({
    "completed": INTEGER, "total": INTEGER, "passed": INTEGER,
    "failed": INTEGER, "skipped": INTEGER, "currentTest": STRING,
    "currentTestElapsed": NUMBER, "stuckSuspected": BOOLEAN,
    "failuresSoFar": exact_array(TEST_FAILURE), "totalFailuresSoFar": INTEGER,
    "failuresTruncated": BOOLEAN, "blockedReason": STRING,
    "editorActive": BOOLEAN,
}, ("completed", "total", "passed", "failed", "skipped"))
TEST_SUMMARY = exact_object({
    "total": INTEGER, "passed": INTEGER, "failed": INTEGER,
    "skipped": INTEGER, "duration": NUMBER,
}, ("total", "passed", "failed", "skipped", "duration"))
TEST_RESULT = exact_object({
    "name": STRING, "fullName": STRING, "status": STRING,
    "duration": NUMBER, "message": STRING, "stackTrace": STRING,
}, ("name", "fullName", "status", "duration"))
ASSET_COPY_RESULT = exact_object({
    "sourcePath": STRING, "targetPath": STRING, "targetGuid": STRING,
}, ("sourcePath", "targetPath"))
EXECUTE_CODE_RESULT = exact_object({
    "result": JSON_VALUE, "truncated": BOOLEAN, "serializedItems": INTEGER,
    "maxResultItems": INTEGER, "maxResultDepth": INTEGER,
    "maxResultStringLength": INTEGER, "count": INTEGER,
    "collectibleAssemblyContext": BOOLEAN, "assemblyIsolation": STRING,
    "assemblyIsolationReason": STRING, "assemblyIsolationWarning": STRING,
}, ("result", "truncated", "serializedItems", "maxResultItems",
    "maxResultDepth", "maxResultStringLength"))
PROJECT_BUILD_SCENE = exact_object({
    "path": STRING, "enabled": BOOLEAN,
}, ("path", "enabled"))
SCENE_SEARCH_RESULT = exact_object({
    "name": STRING, "path": STRING, "instanceId": STRING,
    "active": BOOLEAN, "tag": STRING, "layer": STRING,
    "layerIndex": INTEGER, "scene": STRING,
}, ("name", "path", "instanceId", "active", "tag", "layer",
    "layerIndex", "scene"))
COMPONENT_COUNT = exact_object({
    "type": STRING, "count": INTEGER,
}, ("type", "count"))
TERRAIN_TREE_INSTANCE = exact_object({
    "prototypeIndex": INTEGER, "position": VECTOR3,
    "widthScale": NUMBER, "heightScale": NUMBER,
}, ("prototypeIndex", "position", "widthScale", "heightScale"))
DUPLICATE_ASSET = exact_object({
    "path": STRING, "guid": STRING, "fileSize": INTEGER,
}, ("path", "guid", "fileSize"))
DUPLICATE_GROUP = exact_object({
    "hash": STRING, "width": INTEGER, "height": INTEGER,
    "assetCount": INTEGER, "assets": exact_array(DUPLICATE_ASSET),
}, ("hash", "width", "height", "assetCount", "assets"))
VFX_SETTING = exact_object({
    "name": STRING, "type": STRING, "value": JSON_VALUE,
    "visibility": STRING, "enumValues": STRING_ARRAY,
    "readOnly": BOOLEAN, "available": BOOLEAN, "mutable": BOOLEAN,
}, ("name", "type", "value", "visibility", "enumValues"))
VFX_ASSET_SETTING = exact_object({
    "name": STRING, "type": STRING, "value": JSON_VALUE,
    "available": BOOLEAN, "mutable": BOOLEAN, "owner": STRING,
    "serializedPath": STRING, "minimum": JSON_VALUE,
    "maximum": JSON_VALUE, "enumValues": STRING_ARRAY,
}, ("name", "type", "value", "available", "mutable", "owner",
    "serializedPath", "minimum", "maximum", "enumValues"))
VFX_CATEGORY = exact_object({
    "index": INTEGER, "name": STRING, "collapsed": BOOLEAN,
}, ("index", "name", "collapsed"))
VFX_CUSTOM_ATTRIBUTE = exact_object({
    "index": INTEGER, "name": STRING, "type": STRING,
    "description": STRING, "readOnly": BOOLEAN, "expanded": BOOLEAN,
    "usedInSubgraphs": STRING_ARRAY, "inUse": BOOLEAN,
}, ("index", "name", "type", "description", "readOnly", "expanded",
    "usedInSubgraphs", "inUse"))
VFX_COMPONENT_IDENTITY = exact_object({
    "scope": STRING, "prefabPath": STRING, "scenePath": STRING,
    "hierarchyPath": STRING, "hierarchyIndexPath": STRING,
    "componentIndex": INTEGER,
    "gameObjectInstanceId": STRING, "componentInstanceId": STRING,
}, ("scope", "prefabPath", "scenePath", "hierarchyPath",
    "hierarchyIndexPath",
    "componentIndex", "gameObjectInstanceId", "componentInstanceId"))
VFX_SETTING_RANGE = exact_object({
    "min": JSON_VALUE, "max": JSON_VALUE,
    "minInclusive": BOOLEAN, "maxInclusive": BOOLEAN,
}, ("min", "max", "minInclusive", "maxInclusive"))
VFX_SETTING_SUMMARY = exact_object({
    "scope": STRING, "name": STRING, "serializedProperty": STRING,
    "editorPrefsKey": STRING, "type": STRING, "value": JSON_VALUE,
    "defaultValue": JSON_VALUE, "available": BOOLEAN,
    "mutable": BOOLEAN,
    "documented": BOOLEAN,
    "range": one_of(NULL, VFX_SETTING_RANGE), "enumValues": STRING_ARRAY,
    "requiresGraphReimport": BOOLEAN, "persistenceOwner": STRING,
}, ("scope", "name", "type", "value", "range",
    "available", "mutable", "requiresGraphReimport", "persistenceOwner"))
VFX_COMPONENT_CONTROL_COMPLETION = exact_object({
    "mode": STRING,
    "effectUpdateObserved": BOOLEAN,
    "editorUpdateCount": INTEGER,
    "elapsedMs": NUMBER,
    "expectedTimeDelta": one_of(NULL, NUMBER),
    "observedTimeDelta": one_of(NULL, NUMBER),
}, ("mode", "effectUpdateObserved", "editorUpdateCount", "elapsedMs",
    "expectedTimeDelta", "observedTimeDelta"))


# Reviewed dynamic leaves are explicit and local: the route still has a closed result object,
# while data whose shape is selected by an operation discriminator or external Unity package
# is represented by the recursive JSON value union instead of an empty schema.
OUTPUT_PROPERTY_OVERRIDES: dict[tuple[str, str], dict[str, object]] = {
    ("addressables/build", "result"): JSON_VALUE,
    ("addressables/info", "defaultGroup"): STRING,
    ("addressables/info", "entries"): exact_array(ADDRESSABLE_ENTRY),
    ("addressables/info", "labels"): STRING_ARRAY,
    ("addressables/transaction", "defaultGroup"): STRING,
    ("addressables/transaction", "operations"): exact_array(ADDRESSABLE_OPERATION_RESULT),
    ("addressables/transaction", "results"): exact_array(ADDRESSABLE_OPERATION_RESULT),
    ("animation/update-state", "cycleOffset"): NUMBER,
    ("animation/validate-controller", "issues"): exact_array(ANIMATION_ISSUE),
    ("asmdef/set-platforms", "excludePlatforms"): STRING_ARRAY,
    ("asmdef/set-platforms", "includePlatforms"): STRING_ARRAY,
    ("asset/dependencies", "references"): exact_array(ASSET_REFERENCE),
    ("asset/copy", "copies"): exact_array(ASSET_COPY_RESULT),
    ("asset/import", "imports"): exact_array(BATCH_IMPORT_RESULT),
    ("asset/import", "execution"): EXECUTION_RESULT,
    ("asset/import-settings/set", "requested"): JSON_MAP,
    ("asset/import-settings/set", "requestedPlatformSettings"): JSON_MAP,
    ("asset/import-unitypackage", "completionConfirmedBy"): STRING_ARRAY,
    ("asset/import-unitypackage", "newAssetPaths"): STRING_ARRAY,
    ("asset/list", "assets"): exact_array(ASSET_LIST_ITEM),
    ("asset/move", "moves"): exact_array(BATCH_MOVE_RESULT),
    ("asset/move", "execution"): EXECUTION_RESULT,
    ("asset/refresh", "result"): JSON_VALUE,
    ("asset/rename", "subAssets"): JSON_ARRAY,
    ("asset/transaction", "result"): JSON_VALUE,
    ("audio-mixer/info", "exposedParameters"): JSON_ARRAY,
    ("audio-mixer/info", "effects"): JSON_ARRAY,
    ("audio-mixer/info", "groups"): JSON_ARRAY,
    ("audio-mixer/info", "snapshots"): JSON_ARRAY,
    ("audio-mixer/info", "serializedGraph"): JSON_MAP,
    ("build/get-job", "result"): JSON_VALUE,
    ("build/profile", "globalScenes"): exact_array(BUILD_SCENE),
    ("build/profile", "profiles"): exact_array(BUILD_PROFILE),
    ("build/profile", "operations"): exact_array(BUILD_PROFILE_OPERATION),
    ("build/profile", "results"): exact_array(BUILD_PROFILE_OPERATION),
    ("build/profile", "after"): BUILD_PROFILE_INFO,
    ("build/start", "result"): JSON_VALUE,
    ("cinemachine/transaction", "results"): JSON_ARRAY,
    ("cinemachine/info", "components"): JSON_ARRAY,
    ("cinemachine/transaction", "operations"): JSON_ARRAY,
    ("component/set-reference", "execution"): JSON_MAP,
    ("component/set-reference", "results"): JSON_ARRAY,
    ("console/query", "entries"): exact_array(CONSOLE_ENTRY),
    ("debugger/event-details", "batchBreakCause"): STRING,
    ("debug/evaluate", "result"): EXECUTE_CODE_RESULT,
    ("debugger/events", "currentEvent"): JSON_VALUE,
    ("graphics/compare-images", "averageDelta"): NUMBER,
    ("graphics/compare-images", "differentRatio"): NUMBER,
    ("graphics/material-info", "enabledKeywords"): STRING_ARRAY,
    ("graphics/renderer-info", "materials"): exact_array(RENDERER_MATERIAL),
    ("graphics/rect-gap", "gap"): NUMBER,
    ("graphics/rect-gap", "overlap"): NUMBER,
    ("jobs/list", "jobs"): JSON_ARRAY,
    ("localization/entries", "entries"): JSON_ARRAY,
    ("localization/collections", "collections"): exact_array(LOCALIZATION_COLLECTION),
    ("localization/create-collection", "tables"): exact_array(LOCALIZATION_TABLE),
    ("localization/locales", "locales"): JSON_ARRAY,
    ("localization/remove-entry", "removed"): BOOLEAN,
    ("localization/variables", "groups"): exact_array(LOCALIZATION_VARIABLE_GROUP),
    ("localization/upsert-entry", "createdTables"): STRING_ARRAY,
    ("localization/upsert-entry", "entries"): JSON_ARRAY,
    ("localization/upsert-entry", "errors"): JSON_ARRAY,
    ("localization/settings", "startupLocaleSelectors"): STRING_ARRAY,
    ("localization/status", "startupLocaleSelectors"): STRING_ARRAY,
    ("localization/validate", "issues"): exact_array(LOCALIZATION_ISSUE),
    ("lod/create", "lodLevels"): INTEGER,
    ("lod/info", "size"): NUMBER,
    ("material/properties/set", "requestedProperties"): JSON_MAP,
    ("material/properties/get", "material"): MATERIAL_INFO,
    ("material/properties/set", "before"): MATERIAL_INFO,
    ("material/properties/set", "after"): MATERIAL_INFO,
    ("packages/resolve", "result"): JSON_VALUE,
    ("packages/info", "dependencies"): STRING_ARRAY,
    ("packages/list", "packages"): exact_array(PACKAGE_LIST_ITEM),
    ("packages/search", "results"): exact_array(PACKAGE_SEARCH_ITEM),
    ("packages/update-git", "result"): JSON_VALUE,
    ("packages/lint-metas", "packages"): exact_array(PACKAGE_META_RESULT),
    ("particle/info", "main"): PARTICLE_MAIN,
    ("particle/info", "subEmitters"): JSON_ARRAY,
    ("particle/info", "trails"): JSON_MAP,
    ("physics/overlap-box", "colliders"): exact_array(PHYSICS_COLLIDER),
    ("physics/overlap-sphere", "colliders"): exact_array(PHYSICS_COLLIDER),
    ("physics/raycast", "hits"): exact_array(PHYSICS_HIT),
    ("physics/collision-matrix", "matrix"): JSON_ARRAY,
    ("prefab-asset/add-component", "configuredProperties"): STRING_ARRAY,
    ("prefab-asset/find", "results"): JSON_ARRAY,
    ("prefab-asset/hierarchy", "hierarchy"): PREFAB_HIERARCHY_NODE,
    ("prefab-asset/set-property", "saveException"): STRING,
    ("prefab-asset/set-property", "warnings"): STRING_ARRAY,
    ("prefab-asset/transfer-variant-overrides", "transferredOverrides"): JSON_ARRAY,
    ("prefab-asset/apply-variant-override", "appliedCount"): one_of(STRING, INTEGER),
    ("prefab-asset/revert-variant-override", "revertedCount"): one_of(STRING, INTEGER),
    ("profiler/frame-data", "items"): JSON_ARRAY,
    ("profiler/memory-snapshot-status", "completedUtc"): STRING,
    ("profiler/memory-snapshot-status", "startedUtc"): STRING,
    ("profiler/memory-top-assets", "assets"): JSON_ARRAY,
    ("profiler/memory", "monoFragmentationPercent"): NUMBER,
    ("profiler/memory-breakdown", "categories"): JSON_ARRAY,
    ("profiler/memory-status", "availableCommands"): STRING_ARRAY,
    ("scene/hierarchy", "hierarchy"): JSON_ARRAY,
    ("scene/hierarchy", "matches"): JSON_ARRAY,
    ("scene/workspace", "scenes"): JSON_ARRAY,
    ("project/info", "buildScenes"): exact_array(PROJECT_BUILD_SCENE),
    ("search/scene", "results"): exact_array(SCENE_SEARCH_RESULT),
    ("search/scene-stats", "topComponents"): exact_array(COMPONENT_COUNT),
    ("scriptableobject/list-types", "types"): exact_array(SCRIPTABLE_OBJECT_TYPE),
    ("sceneview/info", "size"): NUMBER,
    ("selection/focus-scene-view", "size"): NUMBER,
    ("serialized-object/set", "beforeValue"): JSON_VALUE,
    ("serialized-object/set", "afterValue"): JSON_VALUE,
    ("serialized-object/set", "property"): one_of(NULL, SERIALIZED_PROPERTY_INFO),
    ("search/missing-references", "results"): exact_array(MISSING_REFERENCE),
    ("settings/player", "applicationIdentifier"): STRING,
    ("shadergraph/status", "availableCommands"): STRING_ARRAY,
    ("shadergraph/get-edges", "edges"): exact_array(SHADER_EDGE),
    ("shadergraph/get-properties", "properties"): JSON_ARRAY,
    ("shadergraph/info", "properties"): JSON_ARRAY,
    ("sprite/sheet-info", "sprites"): exact_array(SPRITE_INFO),
    ("sprite/sheet-info", "textureHeight"): INTEGER,
    ("sprite/sheet-info", "textureWidth"): INTEGER,
    ("sprite/slice-sheet", "sprites"): exact_array(SPRITE_INFO),
    ("sprite/update-animation-clip", "sprites"): STRING_ARRAY,
    ("sprite/pixel-check", "results"): exact_array(SPRITE_PIXEL_RESULT),
    ("textcore/sprite-asset/upsert-images", "atlasWidth"): INTEGER,
    ("textcore/sprite-asset/upsert-images", "atlasHeight"): INTEGER,
    ("textcore/sprite-asset/upsert-images", "spriteWidth"): INTEGER,
    ("textcore/sprite-asset/upsert-images", "spriteHeight"): INTEGER,
    ("textcore/sprite-asset/upsert-images", "packingPadding"): INTEGER,
    ("textcore/sprite-asset/upsert-images", "sprites"):
        exact_array(TEXTCORE_SPRITE_UPSERT_RESULT),
    ("textcore/sprite-asset/upsert-images", "spriteAssetSha256"): STRING,
    ("textcore/sprite-asset/upsert-images", "spriteAssetMetaSha256"): STRING,
    ("textcore/sprite-asset/upsert-images", "atlasSha256"): STRING,
    ("textcore/sprite-asset/upsert-images", "atlasMetaSha256"): STRING,
    ("sprite/replace-slice-update-clip", "animationClip"): ANIMATION_CLIP_UPDATE,
    ("spriteatlas/info", "packingSettings"): SPRITE_ATLAS_PACKING_SETTINGS,
    ("testing/get-job", "availableJobs"): STRING_ARRAY,
    ("testing/get-job", "progress"): TEST_PROGRESS,
    ("testing/get-job", "summary"): TEST_SUMMARY,
    ("testing/get-job", "tests"): exact_array(TEST_RESULT),
    ("testing/list-tests", "tests"): JSON_ARRAY,
    ("terrain/get-tree-instances", "trees"): exact_array(TERRAIN_TREE_INSTANCE),
    ("terrain/get-heights-region", "suggestedStep"): INTEGER,
    ("terrain/get-steepness", "normal"): VECTOR3,
    ("terrain/info", "size"): VECTOR3,
    ("terrain/list", "terrains"): JSON_ARRAY,
    ("terrain/resize", "size"): VECTOR3,
    ("texture/apply-sprite-preset", "updated"): JSON_VALUE,
    ("texture/find-duplicates", "errors"): STRING_ARRAY,
    ("texture/find-duplicates", "extensions"): STRING_ARRAY,
    ("texture/find-duplicates", "groups"): exact_array(DUPLICATE_GROUP),
    ("texture/check-ui-import-settings", "checks"): JSON_ARRAY,
    ("texture/check-import-settings", "results"): exact_array(TEXTURE_IMPORT_CHECK_RESULT),
    ("texture/check-ui-import-settings", "results"): exact_array(TEXTURE_IMPORT_CHECK_RESULT),
    ("texture/info", "convertToNormalmap"): BOOLEAN,
    ("texture/import-image", "skipped"): BOOLEAN,
    ("timeline/info", "tracks"): JSON_ARRAY,
    ("timeline/info", "tracksTruncated"): BOOLEAN,
    ("timeline/info", "serializedGraph"): JSON_MAP,
    ("timeline/transaction", "after"): JSON_MAP,
    ("timeline/transaction", "operations"): JSON_ARRAY,
    ("timeline/transaction", "results"): JSON_ARRAY,
    ("uitoolkit/assert-layout", "results"): JSON_ARRAY,
    ("uitoolkit/assert-layout", "document"): RUNTIME_UI_DOCUMENT,
    ("uitoolkit/asset-inspect", "elements"): exact_array(UXML_ELEMENT),
    ("uitoolkit/asset-inspect", "nameChecks"): exact_array(UXML_NAME_CHECK),
    ("uitoolkit/asset-inspect", "query"): UXML_QUERY,
    ("uitoolkit/asset-inspect", "ussClasses"): {
        "type": "object", "additionalProperties": USS_CLASS_STYLE,
    },
    ("uitoolkit/asset-inspect", "ussPaths"): STRING_ARRAY,
    ("uitoolkit/authoring-transaction", "results"): JSON_ARRAY,
    ("uitoolkit/generated-children", "children"): JSON_ARRAY,
    ("uitoolkit/locate-element", "window"): STRING,
    ("uitoolkit/query", "results"): JSON_ARRAY,
    ("uitoolkit/runtime-documents", "documents"): exact_array(RUNTIME_UI_DOCUMENT),
    ("uitoolkit/builder-preview", "window"): one_of(NULL, WINDOW_INFO),
    ("uitoolkit/builder-preview", "preview"): UI_BUILDER_PREVIEW,
    ("uitoolkit/builder-preview", "canvasAdjustment"): UI_BUILDER_CANVAS_ADJUSTMENT,
    ("uitoolkit/builder-preview", "visualAnalysis"): UI_BUILDER_VISUAL_ANALYSIS,
    ("uitoolkit/builder-preview", "screenshot"): JSON_VALUE,
    ("uitoolkit/capture-element", "context"): UI_CONTEXT,
    ("uitoolkit/capture-element", "cropRect"): RECT,
    ("uitoolkit/capture-element", "element"): UI_ELEMENT_INFO,
    ("uitoolkit/capture-element", "windowCapture"): JSON_VALUE,
    ("uitoolkit/compare-element", "capture"): UI_CAPTURE_RESULT,
    ("uitoolkit/compare-element", "comparison"): IMAGE_COMPARISON,
    ("uitoolkit/edit-uxml", "results"): JSON_ARRAY,
    ("uitoolkit/generated-children", "context"): UI_CONTEXT,
    ("uitoolkit/generated-children", "element"): UI_ELEMENT_INFO,
    ("uitoolkit/locate-element", "context"): UI_CONTEXT,
    ("uitoolkit/locate-element", "cropRect"): RECT,
    ("uitoolkit/locate-element", "element"): UI_ELEMENT_INFO,
    ("uitoolkit/locate-element", "panelRect"): RECT,
    ("uitoolkit/resource-audit", "context"): UI_CONTEXT,
    ("uitoolkit/runtime-query", "results"): JSON_ARRAY,
    ("uitoolkit/runtime-query", "document"): RUNTIME_UI_DOCUMENT,
    ("uitoolkit/runtime-repaint", "document"): RUNTIME_UI_DOCUMENT,
    ("uitoolkit/runtime-style", "background"): UI_BACKGROUND,
    ("uitoolkit/runtime-style", "document"): RUNTIME_UI_DOCUMENT,
    ("uitoolkit/runtime-style", "element"): UI_ELEMENT_INFO,
    ("uitoolkit/runtime-style", "inlineStyle"): UI_INLINE_STYLE,
    ("uitoolkit/runtime-style", "resolvedStyle"): UI_RESOLVED_STYLE,
    ("uitoolkit/runtime-tree", "document"): RUNTIME_UI_DOCUMENT,
    ("uitoolkit/runtime-tree", "tree"): UI_ELEMENT_INFO,
    ("uitoolkit/diagnose-runtime", "document"): RUNTIME_UI_DOCUMENT,
    ("uitoolkit/query", "window"): WINDOW_INFO,
    ("uitoolkit/repaint", "window"): WINDOW_INFO,
    ("uitoolkit/style", "inlineStyle"): UI_INLINE_STYLE,
    ("uitoolkit/style", "window"): WINDOW_INFO,
    ("uitoolkit/style", "element"): UI_ELEMENT_INFO,
    ("uitoolkit/style", "resolvedStyle"): UI_RESOLVED_STYLE,
    ("uitoolkit/tree", "window"): WINDOW_INFO,
    ("uitoolkit/tree", "tree"): UI_ELEMENT_INFO,
    ("uitoolkit/visual-check", "document"): RUNTIME_UI_DOCUMENT,
    ("uitoolkit/windows", "windows"): exact_array(WINDOW_INFO),
    ("undo/perform", "undoGroup"): INTEGER,
    ("vfxgraph/catalog", "includeExperimental"): BOOLEAN,
    ("vfxgraph/catalog", "maxSettingsPerItem"): INTEGER,
    ("vfxgraph/catalog", "maxSlotsPerItem"): INTEGER,
    ("vfxgraph/catalog", "items"): JSON_ARRAY,
    ("vfxgraph/catalog", "capabilities"): JSON_ARRAY,
    ("vfxgraph/catalog", "relatedRoutes"): JSON_ARRAY,
    ("vfxgraph/component-control", "target"): VFX_COMPONENT_IDENTITY,
    ("vfxgraph/component-control", "state"): JSON_MAP,
    ("vfxgraph/component-info", "components"): JSON_ARRAY,
    ("vfxgraph/component-info", "total"): INTEGER,
    ("vfxgraph/component-transaction", "target"): VFX_COMPONENT_IDENTITY,
    ("vfxgraph/component-transaction", "component"): JSON_MAP,
    ("vfxgraph/component-transaction", "results"): JSON_ARRAY,
    ("vfxgraph/create", "assetKind"): STRING,
    ("vfxgraph/info", "assetKind"): STRING,
    ("vfxgraph/info", "compilationMode"): one_of(NULL, STRING),
    ("vfxgraph/info", "graphSettings"): exact_array(VFX_SETTING),
    ("vfxgraph/info", "resourceSettings"): JSON_MAP,
    ("vfxgraph/info", "resourceSettingDescriptors"):
        exact_array(VFX_ASSET_SETTING),
    ("vfxgraph/info", "events"): STRING_ARRAY,
    ("vfxgraph/info", "dependencies"): STRING_ARRAY,
    ("vfxgraph/info", "dataObjects"): JSON_ARRAY,
    ("vfxgraph/info", "nodes"): JSON_ARRAY,
    ("vfxgraph/info", "parameters"): JSON_ARRAY,
    ("vfxgraph/info", "exposedProperties"): JSON_ARRAY,
    ("vfxgraph/info", "categories"): exact_array(VFX_CATEGORY),
    ("vfxgraph/info", "customAttributes"): exact_array(VFX_CUSTOM_ATTRIBUTE),
    ("vfxgraph/info", "connections"): JSON_ARRAY,
    ("vfxgraph/info", "uiItems"): JSON_ARRAY,
    ("vfxgraph/info", "diagnostics"): JSON_ARRAY,
    ("vfxgraph/info", "serializedGraph"): JSON_MAP,
    ("vfxgraph/settings-info", "project"): exact_array(VFX_SETTING_SUMMARY),
    ("vfxgraph/settings-info", "user"): exact_array(VFX_SETTING_SUMMARY),
    ("vfxgraph/settings-transaction", "settings"): exact_object({
        "project": exact_array(VFX_SETTING_SUMMARY),
        "user": exact_array(VFX_SETTING_SUMMARY),
    }, ("project", "user")),
    ("vfxgraph/settings-transaction", "results"): JSON_ARRAY,
    ("vfxgraph/transaction", "assetKind"): STRING,
    ("vfxgraph/transaction", "aliases"): STRING_MAP,
    ("vfxgraph/transaction", "idRemap"): STRING_MAP,
    ("vfxgraph/transaction", "operations"): JSON_ARRAY,
    ("vfxgraph/transaction", "results"): JSON_ARRAY,
    ("vfxgraph/transaction", "deferredChecks"): STRING_ARRAY,
    ("vfxgraph/validate", "assetKind"): STRING,
    ("vfxgraph/validate", "diagnostics"): JSON_ARRAY,
    ("vfxgraph/validate", "systems"): STRING_ARRAY,
    ("vfxgraph/validate", "events"): STRING_ARRAY,
    ("vfxgraph/validate", "exposedProperties"): JSON_ARRAY,
    ("vfxgraph/validate", "shaders"): JSON_ARRAY,
    ("vfxgraph/validate", "dependencies"): STRING_ARRAY,
    ("vfxgraph/validate", "compileOutput"): one_of(NULL, JSON_MAP),
}


QUEUE_AVAILABILITY_PROPERTIES = {
    "queueReady": BOOLEAN, "durableAdmissionReady": BOOLEAN,
    "editorLoopAdvancing": BOOLEAN, "editorLoopAgeMs": NULLABLE_NUMBER,
    "editorLoopPulse": INTEGER, "lastEditorUpdateAt": STRING,
    "editorApplicationActive": BOOLEAN, "busyReason": STRING,
}
JOB_SNAPSHOT = exact_object({
    "jobId": STRING, "jobAccessToken": STRING, "jobType": STRING,
    "operation": STRING, "status": STRING, "tags": STRING_ARRAY,
    "cleanupStatus": STRING, "cleanupToken": STRING, "progress": NUMBER,
    "statusMessage": STRING, "phase": STRING, "pollRoute": STRING,
    "recoveredAfterReload": BOOLEAN, "domainReloadCount": INTEGER,
    "blockedReason": STRING, "stepCount": INTEGER, "nextRunAt": STRING,
    "idempotencyKey": STRING, "createdAt": STRING, "startedAt": STRING,
    "completedAt": STRING, "updatedAt": STRING, "sideEffects": STRING_ARRAY,
    "result": JSON_VALUE, "error": JSON_VALUE,
    "cleanupResult": JSON_VALUE, "cleanupError": JSON_VALUE,
}, ("jobId", "jobType", "operation", "status", "createdAt", "updatedAt"))

TRANSACTION_TERMINAL_STATE = string_enum(
    "committed", "rolled_back", "rollback_failed", "outcome_uncertain")
TRANSACTION_EVIDENCE = exact_array(exact_object({
    "kind": STRING, "path": STRING, "exists": BOOLEAN,
    "assetSha256": STRING, "metaExists": BOOLEAN, "metaSha256": STRING,
}, ("kind", "path", "exists")))
TRANSACTION_COMPILATION_EVIDENCE = exact_object({
    "requested": BOOLEAN, "started": BOOLEAN, "finished": BOOLEAN,
    "assemblyReloadObserved": BOOLEAN, "compilerErrorCount": INTEGER,
    "compilerWarningCount": INTEGER,
})
BASE_ERROR = exact_object({
    "success": BOOLEAN, "error": STRING, "message": STRING,
    "errorCode": STRING, "retryable": BOOLEAN,
    "compilerMessages": JSON_ARRAY,
}, ("success", "error", "errorCode", "retryable"))
ASSET_TRANSACTION_ERROR_CODES = (
    "asset_transaction_failed",
    "asset_transaction_interrupted_before_publish",
    "asset_transaction_interrupted_during_apply",
    "asset_transaction_interrupted_during_rollback",
    "asset_transaction_prepare_interrupted",
    "compilation_evidence_incomplete",
    "compilation_failed",
    "idempotency_conflict",
    "invalid_arguments",
    "invalid_operation",
    "job_owner_mismatch",
    "loaded_scene_asset_mutation",
    "outcome_uncertain",
    "response_too_large",
    "rollback_failed",
    "target_project_required",
    "tool_execution_failed",
    "transaction_postcondition_failed",
    "transaction_preflight_failed",
    "transaction_snapshot_invalid",
    "wrong_unity_project",
)
ASSET_TRANSACTION_ERROR = exact_object({
    "success": BOOLEAN,
    "error": STRING,
    "message": STRING,
    "errorCode": string_enum(*ASSET_TRANSACTION_ERROR_CODES),
    "retryable": BOOLEAN,
    "terminalState": TRANSACTION_TERMINAL_STATE,
    "rollbackVerified": BOOLEAN,
    "originalError": BASE_ERROR,
    "rollbackErrors": STRING_ARRAY,
    "differences": STRING_ARRAY,
    "requiresReconciliation": BOOLEAN,
    "compilerMessages": JSON_ARRAY,
    "route": STRING,
    "expectedProjectPath": STRING,
    "actualProjectPath": STRING,
    "actualProjectName": STRING,
    "actualPort": INTEGER,
    "currentInstance": JSON_VALUE,
    "actualBytes": INTEGER,
    "limitBytes": INTEGER,
    "jobId": STRING,
    "operation": STRING,
}, ("success", "error", "errorCode", "retryable"))
ASSET_TRANSACTION_TERMINAL_RESULT = exact_object({
    "transactionId": STRING,
    "terminalState": TRANSACTION_TERMINAL_STATE,
    "operationCount": INTEGER,
    "completedOperationCount": INTEGER,
    "results": JSON_ARRAY,
    "baselineEvidence": TRANSACTION_EVIDENCE,
    "commitEvidence": TRANSACTION_EVIDENCE,
    "rollbackVerified": BOOLEAN,
    "compilationEvidence": TRANSACTION_COMPILATION_EVIDENCE,
}, ("transactionId", "terminalState", "operationCount",
    "completedOperationCount", "results", "baselineEvidence"))
ASSET_TRANSACTION_JOB = exact_object({
    **JOB_SNAPSHOT["properties"],
    "result": ASSET_TRANSACTION_TERMINAL_RESULT,
    "error": ASSET_TRANSACTION_ERROR,
    "transactionId": STRING,
    "terminalState": TRANSACTION_TERMINAL_STATE,
}, tuple(JOB_SNAPSHOT["required"]) + ("transactionId",))

COMPILATION_ENTRY = exact_object({
    "file": STRING, "line": INTEGER, "column": INTEGER,
    "message": STRING, "severity": string_enum("error", "warning"),
    "code": STRING, "isDeprecated": BOOLEAN, "assembly": STRING,
    "timestamp": STRING,
}, ("file", "line", "column", "message", "severity", "code",
    "isDeprecated", "assembly", "timestamp"))
JOB_HISTORY_ENTRY = exact_object({
    "jobId": STRING, "jobType": STRING, "ownerAgentId": STRING,
    "status": STRING, "updatedAt": STRING, "snapshot": JSON_VALUE,
}, ("jobId", "jobType", "ownerAgentId", "status", "updatedAt", "snapshot"))


REUSABLE_OUTPUT_SCHEMAS = {
    "NullableRect": NULLABLE_UI_RECT,
    "SerializedPropertyInfo": SERIALIZED_PROPERTY_INFO,
    "WindowInfo": WINDOW_INFO,
}

QUEUE_TICKET = exact_object({
    "ticketId": INTEGER, "actionName": STRING, "status": STRING,
    "submittedAt": STRING, "queuePosition": INTEGER, "startedAt": STRING,
    "queueWaitTimeMs": INTEGER, "progress": JSON_VALUE, "completedAt": STRING,
    "executionTimeMs": INTEGER, "result": JSON_VALUE,
}, ("ticketId", "actionName", "status", "submittedAt"))
AUDIT_REPORT_COMMON = {
    "passed": BOOLEAN, "warningCount": INTEGER, "suppressedCount": INTEGER,
    "truncated": BOOLEAN, "issues": JSON_ARRAY, "errors": STRING_ARRAY,
    "scope": JSON_MAP, "automaticAudit": JSON_MAP, "selfTests": JSON_MAP,
}
PROJECT_AUDITOR_ISSUE = exact_object({
    "descriptorId": STRING, "category": STRING, "severity": STRING,
    "logLevel": STRING, "description": STRING, "path": STRING,
    "line": INTEGER, "customProperties": STRING_ARRAY,
}, ("descriptorId", "category", "severity", "logLevel", "description",
    "path", "line"))

OUTPUT_SCHEMA_OVERRIDES: dict[str, list[dict[str, object]]] = {
    "asset/refresh": [JOB_SNAPSHOT],
    "asmdef/info": [exact_object({
        "_filePath": STRING, "name": STRING, "rootNamespace": STRING,
        "references": STRING_ARRAY, "includePlatforms": STRING_ARRAY,
        "excludePlatforms": STRING_ARRAY, "allowUnsafeCode": BOOLEAN,
        "overrideReferences": BOOLEAN, "precompiledReferences": STRING_ARRAY,
        "autoReferenced": BOOLEAN, "defineConstraints": STRING_ARRAY,
        "versionDefines": exact_array(exact_object({
            "name": STRING, "expression": STRING, "define": STRING,
        }, ("name", "expression", "define"))),
        "noEngineReferences": BOOLEAN,
    }, ("_filePath", "name"))],
    "audio-mixer/transaction": [exact_object({
        "dryRun": BOOLEAN, "assetPath": STRING, "operationCount": INTEGER,
        "operations": JSON_ARRAY, "results": JSON_ARRAY,
    }, ("dryRun", "assetPath", "operationCount"))],
    "vfxgraph/component-control": [exact_object({
        "action": STRING,
        "target": VFX_COMPONENT_IDENTITY,
        "stateBefore": JSON_MAP,
        "state": JSON_MAP,
        "completion": VFX_COMPONENT_CONTROL_COMPLETION,
    }, ("action", "target", "stateBefore", "state", "completion"))],
    "vfxgraph/bake": [exact_object({
        "kind": STRING, "outputPath": STRING, "guid": STRING,
        "overwritten": BOOLEAN,
        "sourceMeshPath": STRING,
        "dimensions": exact_object({
            "x": INTEGER, "y": INTEGER, "z": INTEGER,
        }, ("x", "y", "z")),
        "actualBoxSize": VECTOR3, "boxCenter": VECTOR3,
        "textureFormat": STRING, "voxelCount": INTEGER,
        "assetType": STRING, "pointCount": INTEGER,
        "properties": JSON_ARRAY,
        "sourceVertexOrPixelCount": INTEGER,
        "sourceTriangleCount": INTEGER,
        "sourceTexturePath": STRING,
        "sourceDimensions": exact_object({
            "x": INTEGER, "y": INTEGER,
        }, ("x", "y")),
        "thresholdMode": STRING, "threshold": NUMBER,
    }, ("kind", "outputPath", "guid", "overwritten"))],
    "asset/transaction": [
        exact_object({
            "dryRun": BOOLEAN, "validation": STRING, "operationCount": INTEGER,
            "operations": JSON_ARRAY, "compilationRequired": BOOLEAN,
        }, ("dryRun", "validation", "operationCount", "operations",
            "compilationRequired")),
        ASSET_TRANSACTION_JOB,
        ASSET_TRANSACTION_ERROR,
    ],
    "compilation/errors": [exact_object({
        "isCompiling": BOOLEAN,
        "counts": exact_object({
            "errors": INTEGER, "warnings": INTEGER,
        }, ("errors", "warnings")),
        "deprecatedWarnings": exact_array(COMPILATION_ENTRY),
        "deprecatedWarningTotal": INTEGER,
        "entries": exact_array(COMPILATION_ENTRY),
        "entryTotal": INTEGER,
    }, ("isCompiling", "counts", "deprecatedWarnings", "entries"))],
    "editor/execute-menu-item": [exact_object({
        "menuPath": STRING,
    }, ("menuPath",))],
    "editor/play-mode": [
        exact_object({
            "action": STRING, "stateConfirmed": BOOLEAN,
            "isPlaying": BOOLEAN, "isPaused": BOOLEAN,
            "changed": BOOLEAN, "stableFrames": INTEGER,
            "elapsedMs": NUMBER,
        }, ("action", "stateConfirmed", "isPlaying", "isPaused",
            "changed", "stableFrames", "elapsedMs")),
        JOB_SNAPSHOT,
    ],
    "editor/play-mode-options": [exact_object({
        "changed": BOOLEAN,
        "previous": PLAY_MODE_OPTIONS_STATE,
        "current": PLAY_MODE_OPTIONS_STATE,
    }, ("changed", "previous", "current"))],
    "packages/resolve": [JOB_SNAPSHOT],
    "packages/update-git": [JOB_SNAPSHOT],
    "vfxgraph/transaction": [
        exact_object({
            "dryRun": BOOLEAN,
            "assetPath": STRING,
            "assetKind": STRING,
            "operationCount": INTEGER,
            "results": JSON_ARRAY,
            "aliases": STRING_MAP,
            "idRemap": STRING_MAP,
            "assetHash": STRING,
            "deferredChecks": STRING_ARRAY,
        }, ("dryRun", "assetPath", "assetKind", "operationCount",
            "results", "aliases", "idRemap", "assetHash",
            "deferredChecks")),
        exact_object({
            "dryRun": BOOLEAN,
            "assetPath": STRING,
            "assetKind": STRING,
            "operationCount": INTEGER,
            "results": JSON_ARRAY,
            "aliases": STRING_MAP,
            "idRemap": STRING_MAP,
            "previousAssetHash": STRING,
            "assetHash": STRING,
            "changed": BOOLEAN,
        }, ("dryRun", "assetPath", "assetKind", "operationCount",
            "results", "aliases", "idRemap", "previousAssetHash",
            "assetHash", "changed")),
    ],
    "editor/execute-code": [JOB_SNAPSHOT],
    "jobs/get": [JOB_SNAPSHOT],
    "jobs/cancel": [JOB_SNAPSHOT],
    "jobs/cleanup": [JOB_SNAPSHOT],
    "jobs/list": [exact_object({
        "ownerAgentId": STRING, "total": INTEGER, "offset": INTEGER,
        "limit": INTEGER, "hasMore": BOOLEAN,
        "nextOffset": NULLABLE_INTEGER,
        "jobs": exact_array(JOB_HISTORY_ENTRY),
    }, ("ownerAgentId", "total", "offset", "limit", "hasMore",
        "nextOffset", "jobs"))],
    "localization/upsert-entry": [exact_object({
        "collection": STRING, "type": STRING, "entryCount": INTEGER,
        "processedCount": INTEGER, "createdKeyCount": INTEGER,
        "createdEntryCount": INTEGER, "updatedEntryCount": INTEGER,
        "createdTableCount": INTEGER, "createdTables": STRING_ARRAY,
        "saved": BOOLEAN, "errors": STRING_ARRAY,
        "entries": exact_array(LOCALIZATION_UPSERT_ENTRY),
        "execution": EXECUTION_RESULT,
    }, ("collection", "type", "entryCount", "processedCount",
        "createdKeyCount", "createdEntryCount", "updatedEntryCount",
        "createdTableCount", "createdTables", "saved", "errors", "entries",
        "execution"))],
    "prefab-asset/find": [exact_object({
        "success": BOOLEAN, "prefab": STRING, "assetPath": STRING,
        "count": INTEGER, "truncated": BOOLEAN, "results": JSON_ARRAY,
    }, ("success", "prefab", "assetPath", "count", "truncated", "results"))],
    "prefab-asset/configure-component": [exact_object({
        "saved": BOOLEAN, "prefab": STRING, "assetPath": STRING,
        "operationCount": INTEGER, "operationSummaries": JSON_ARRAY,
        "transaction": JSON_MAP, "execution": JSON_MAP,
        "prefabFileDiff": JSON_VALUE,
    }, ("saved", "assetPath"))],
    "prefab-asset/transaction-edit": [exact_object({
        "saved": BOOLEAN, "prefab": STRING, "assetPath": STRING,
        "operationCount": INTEGER, "operationSummaries": JSON_ARRAY,
        "transaction": JSON_MAP, "execution": JSON_MAP,
        "prefabFileDiff": JSON_VALUE,
    }, ("saved", "assetPath", "operationCount", "operationSummaries",
        "transaction", "execution"))],
    "profiler/analyze": [exact_object({
        "memory": exact_object({
            "totalAllocatedMB": INTEGER, "monoUsedMB": INTEGER,
            "monoHeapMB": INTEGER, "gfxDriverMB": INTEGER,
        }),
        "rendering": JSON_MAP, "profiler": JSON_MAP, "hotspots": JSON_ARRAY,
        "sceneComplexity": exact_object({
            "gameObjectCount": INTEGER, "rendererCount": INTEGER,
            "lightCount": INTEGER, "realtimeLights": INTEGER,
            "bakedLights": INTEGER, "shadowCastingLights": INTEGER,
        }),
        "suggestions": STRING_ARRAY, "suggestionCount": INTEGER,
    }, ("memory", "rendering", "profiler", "sceneComplexity", "suggestions",
        "suggestionCount"))],
    "profiler/memory-snapshot": [exact_object({
        "jobId": STRING, "jobType": STRING, "status": STRING,
        "apiType": STRING, "captureFlags": STRING, "snapshotPath": STRING,
        "tempPath": STRING, "fileExists": BOOLEAN, "fileSizeBytes": INTEGER,
        "tempFileExists": BOOLEAN, "tempFileSizeBytes": INTEGER,
        "startedUtc": STRING, "completedUtc": STRING, "timedOut": BOOLEAN,
        "cancelRequested": BOOLEAN, "captureMayStillComplete": BOOLEAN,
        "elapsedMs": NUMBER, "error": STRING, "jobAccessToken": STRING,
        "completed": BOOLEAN, "canceled": BOOLEAN,
    }, ("jobId", "jobType", "status", "snapshotPath", "startedUtc"))],
    "profiler/stats": [exact_object({
        **{name: INTEGER for name in (
            "batches", "drawCalls", "indirectDrawCalls", "dynamicBatchedDrawCalls",
            "staticBatchedDrawCalls", "instancedBatchedDrawCalls", "dynamicBatches",
            "staticBatches", "instancedBatches", "setPassCalls", "triangles",
            "vertices", "shadowCasters", "renderTextureChanges", "renderTextureCount",
            "renderTextureBytes", "usedTextureMemorySize", "usedTextureCount",
            "vboTotal", "vboTotalBytes", "vboUploads", "vboUploadBytes",
            "ibUploads", "ibUploadBytes", "visibleSkinnedMeshes",
            "animationComponentsPlaying", "animatorComponentsPlaying")},
        "frameTime": NUMBER, "renderTime": NUMBER, "screenResolution": STRING,
        "isPlaying": BOOLEAN, "note": STRING,
    }, ("isPlaying",))],
    "project-auditor/audit": [exact_object({
        "reportVersion": STRING, "isValid": BOOLEAN, "platform": STRING,
        "elapsedMs": INTEGER, "totalIssues": INTEGER,
        "matchedIssueCount": INTEGER, "offset": INTEGER, "limit": INTEGER,
        "nextOffset": INTEGER, "issues": exact_array(PROJECT_AUDITOR_ISSUE),
    }, ("reportVersion", "isValid", "platform", "elapsedMs", "totalIssues",
        "matchedIssueCount", "offset", "limit", "issues"))],
    "textmeshpro/font-asset/upsert-bitmap-glyphs": [exact_object({
        "fontAssetPath": STRING,
        "atlasWidth": INTEGER,
        "atlasHeight": INTEGER,
        "atlasFormat": STRING,
        "glyphPixelHeight": INTEGER,
        "sdfSpread": INTEGER,
        "packingPadding": INTEGER,
        "glyphs": exact_array(exact_object({
            "unicode": INTEGER,
            "imagePath": STRING,
            "glyphIndex": INTEGER,
            "rect": exact_object({
                "x": INTEGER, "y": INTEGER,
                "width": INTEGER, "height": INTEGER,
            }, ("x", "y", "width", "height")),
            "metrics": exact_object({
                "width": NUMBER, "height": NUMBER,
                "bearingX": NUMBER, "bearingY": NUMBER,
                "advance": NUMBER,
            }, ("width", "height", "bearingX", "bearingY", "advance")),
        }, ("unicode", "imagePath", "glyphIndex", "rect", "metrics"))),
        "assetSha256": STRING,
        "metaSha256": STRING,
    }, ("fontAssetPath", "atlasWidth", "atlasHeight", "atlasFormat",
        "glyphPixelHeight", "sdfSpread", "packingPadding", "glyphs",
        "assetSha256", "metaSha256"))],
    "queue/cancel": [exact_object({
        "ticketId": INTEGER, "status": STRING, "canceledBeforeExecution": BOOLEAN,
    }, ("ticketId", "status", "canceledBeforeExecution"))],
    "queue/info": [exact_object({
        "totalQueued": INTEGER, "activeAgents": INTEGER, "executingCount": INTEGER,
        "completedCacheSize": INTEGER, "perAgentQueued": {
            "type": "object", "additionalProperties": INTEGER,
        }, "totalSessionsTracked": INTEGER, **QUEUE_AVAILABILITY_PROPERTIES,
    }, ("totalQueued", "activeAgents", "executingCount", "completedCacheSize",
        "perAgentQueued", "totalSessionsTracked", "queueReady",
        "durableAdmissionReady", "editorLoopAdvancing", "editorLoopAgeMs",
        "editorLoopPulse", "lastEditorUpdateAt", "editorApplicationActive"))],
    "queue/status": [QUEUE_TICKET],
    "scenario/info": [exact_object({
        "mppmAvailable": BOOLEAN, "mppmVersion": STRING, "isMainEditor": BOOLEAN,
        "tags": STRING_ARRAY, "currentPlayerError": STRING,
    }, ("mppmAvailable", "mppmVersion"))],
    "screenshot/editor-window": [exact_object({
        "path": STRING, "fullPath": STRING, "windowTitle": STRING,
        "width": INTEGER, "height": INTEGER, "sizeBytes": INTEGER,
        "platform": STRING,
    }, ("platform",))],
    "screenshot/game": [exact_object({
        "path": STRING, "fullPath": STRING, "superSize": INTEGER,
        "width": INTEGER, "height": INTEGER, "sizeBytes": INTEGER,
        "waitFrames": INTEGER, "stableFrames": INTEGER, "elapsedMs": NUMBER,
        "fileReady": BOOLEAN,
        "editorOverlayMode": STRING,
        "editorOverlaysSuppressed": BOOLEAN,
        "gameViewGizmosSuppressed": BOOLEAN,
        "gameViewStatsSuppressed": BOOLEAN,
        "sanitizedGameViewCount": INTEGER,
        "editorOverlayStateRestored": BOOLEAN,
        "paused": BOOLEAN,
        "window": STRING,
        "floating": BOOLEAN,
        "coordinateMode": STRING,
        "captureMethod": STRING,
        "contentRect": exact_object({
            "x": INTEGER, "y": INTEGER,
            "width": INTEGER, "height": INTEGER,
        }, ("x", "y", "width", "height")),
        "warning": STRING,
    }, ("path", "fullPath", "superSize", "width", "height", "sizeBytes",
        "waitFrames", "stableFrames", "elapsedMs", "fileReady",
        "editorOverlayMode", "editorOverlaysSuppressed",
        "gameViewGizmosSuppressed", "gameViewStatsSuppressed",
        "sanitizedGameViewCount", "editorOverlayStateRestored"))],
    "uitoolkit/audit-uss-styles": [exact_object({
        **AUDIT_REPORT_COMMON, "scannedStyleSheets": INTEGER,
        "indexedStyleSheets": INTEGER, "indexedUxmlFiles": INTEGER,
        "indexedRuntimeSources": INTEGER, "errorCount": INTEGER,
        "suppressionSyntax": STRING,
        "redundantDeclarationSuppressionSyntax": STRING,
        "pixelGridSuppressionSyntax": STRING,
        "textStyleContractSuppressionSyntax": STRING,
    }, ("passed", "scannedStyleSheets", "indexedStyleSheets", "indexedUxmlFiles",
        "indexedRuntimeSources", "errorCount", "warningCount", "suppressedCount",
        "truncated", "issues", "errors"))],
    "uitoolkit/audit-uxml-layout": [exact_object({
        **AUDIT_REPORT_COMMON, "scannedUxmlFiles": INTEGER,
        "indexedUxmlFiles": INTEGER, "indexedStyleSheets": INTEGER,
        "indexedRuntimeSourceFiles": INTEGER, "indexedSerializedAssetFiles": INTEGER,
        "suppressionSyntax": STRING_ARRAY,
    }, ("passed", "scannedUxmlFiles", "indexedUxmlFiles", "indexedStyleSheets",
        "indexedRuntimeSourceFiles", "indexedSerializedAssetFiles", "warningCount",
        "suppressedCount", "truncated", "issues", "errors"))],
    "uitoolkit/refresh": [exact_object({
        "timedOut": BOOLEAN, "elapsedMs": NUMBER, "frameCount": INTEGER,
        "repaintedRuntimeDocuments": INTEGER, "isCompiling": BOOLEAN,
        "isUpdating": BOOLEAN,
    }, ("timedOut", "elapsedMs", "frameCount", "repaintedRuntimeDocuments",
        "isCompiling", "isUpdating"))],
    "wait/editor-idle": [exact_object({
        "timedOut": BOOLEAN, "isIdle": BOOLEAN, "busyReasons": STRING_ARRAY,
        "lastBusyReasons": STRING_ARRAY, "isCompiling": BOOLEAN,
        "isUpdating": BOOLEAN, "isPlaying": BOOLEAN,
        "isChangingPlayMode": BOOLEAN, "isPlayingOrWillChangePlaymode": BOOLEAN,
        "activeScene": STRING, "activeScenePath": STRING, "timeoutMs": INTEGER,
        "stableFrames": INTEGER, "stableMs": INTEGER,
        "currentStableFrames": INTEGER, "stableDurationMs": INTEGER,
        "elapsedMs": INTEGER, "resumedAfterReload": BOOLEAN,
        "resumeCount": INTEGER, "compilationDiagnostics": JSON_MAP,
    }, ("timedOut", "isIdle", "busyReasons", "lastBusyReasons", "isCompiling",
        "isUpdating", "isPlaying", "isChangingPlayMode",
        "isPlayingOrWillChangePlaymode", "activeScene", "activeScenePath",
        "timeoutMs", "stableFrames", "stableMs", "currentStableFrames",
        "stableDurationMs", "elapsedMs", "resumedAfterReload", "resumeCount",
        "compilationDiagnostics"))],
    "undo/history": [exact_object({
        "count": INTEGER, "total": INTEGER,
        "actions": exact_array(exact_object({
            "id": INTEGER, "requestId": INTEGER, "executionTimeMs": INTEGER,
            "targetInstanceId": INTEGER, "undoGroup": INTEGER,
            "timestamp": STRING, "agentId": STRING, "actionName": STRING,
            "category": STRING, "status": STRING, "errorMessage": STRING,
            "targetPath": STRING, "targetType": STRING, "undoGroupName": STRING,
            "undoStatus": STRING, "undoUnavailableReason": STRING,
            "parameters": JSON_MAP,
        })),
    }, ("count", "total", "actions"))],
}

NON_CATALOG_ROUTES: set[str] = set()


def matching(text: str, start: int, opening: str = "{", closing: str = "}") -> int:
    depth = 0
    state = "code"
    index = start
    while index < len(text):
        character = text[index]
        following = text[index + 1] if index + 1 < len(text) else ""
        if state == "code":
            if character == '"':
                state = "string"
            elif character == "'":
                state = "char"
            elif character == "/" and following == "/":
                state = "line-comment"
                index += 1
            elif character == "/" and following == "*":
                state = "block-comment"
                index += 1
            elif character == opening:
                depth += 1
            elif character == closing:
                depth -= 1
                if depth == 0:
                    return index
        elif state in {"string", "char"}:
            if character == "\\":
                index += 1
            elif state == "string" and character == '"':
                state = "code"
            elif state == "char" and character == "'":
                state = "code"
        elif state == "line-comment":
            if character == "\n":
                state = "code"
        elif state == "block-comment" and character == "*" and following == "/":
            state = "code"
            index += 1
        index += 1
    raise ValueError(f"Unmatched {opening!r} at offset {start}")


def split_top_level(text: str, separator: str = ",") -> list[str]:
    result: list[str] = []
    last = 0
    depths = {"(": 0, "[": 0, "{": 0}
    closing = {")": "(", "]": "[", "}": "{"}
    state = "code"
    index = 0
    while index < len(text):
        character = text[index]
        following = text[index + 1] if index + 1 < len(text) else ""
        if state == "code":
            if character == '"':
                state = "string"
            elif character == "'":
                state = "char"
            elif character == "/" and following == "/":
                state = "line-comment"
                index += 1
            elif character == "/" and following == "*":
                state = "block-comment"
                index += 1
            elif character in depths:
                depths[character] += 1
            elif character in closing:
                depths[closing[character]] -= 1
            elif character == separator and not any(depths.values()):
                result.append(text[last:index].strip())
                last = index + 1
        elif state in {"string", "char"}:
            if character == "\\":
                index += 1
            elif state == "string" and character == '"':
                state = "code"
            elif state == "char" and character == "'":
                state = "code"
        elif state == "line-comment":
            if character == "\n":
                state = "code"
        elif state == "block-comment" and character == "*" and following == "/":
            state = "code"
            index += 1
        index += 1
    result.append(text[last:].strip())
    return [item for item in result if item]


def split_top_level_operator(text: str, operator: str) -> list[str]:
    """Split a C# expression on an operator outside nested expressions."""
    if not operator:
        raise ValueError("operator must not be empty")
    result: list[str] = []
    last = 0
    depths = {"(": 0, "[": 0, "{": 0}
    closing = {")": "(", "]": "[", "}": "{"}
    state = "code"
    index = 0
    while index < len(text):
        character = text[index]
        following = text[index + 1] if index + 1 < len(text) else ""
        if state == "code":
            if character == '"':
                state = "string"
            elif character == "'":
                state = "char"
            elif character == "/" and following == "/":
                state = "line-comment"
                index += 1
            elif character == "/" and following == "*":
                state = "block-comment"
                index += 1
            elif character in depths:
                depths[character] += 1
            elif character in closing:
                depths[closing[character]] -= 1
            elif not any(depths.values()) and text.startswith(operator, index):
                result.append(text[last:index].strip())
                index += len(operator) - 1
                last = index + 1
        elif state in {"string", "char"}:
            if character == "\\":
                index += 1
            elif state == "string" and character == '"':
                state = "code"
            elif state == "char" and character == "'":
                state = "code"
        elif state == "line-comment":
            if character == "\n":
                state = "code"
        elif state == "block-comment" and character == "*" and following == "/":
            state = "code"
            index += 1
        index += 1
    result.append(text[last:].strip())
    return [item for item in result if item]


def strip_balanced_parentheses(text: str) -> str:
    """Remove only parentheses that wrap the complete expression."""
    result = text.strip()
    while result.startswith("("):
        try:
            closing = matching(result, 0, "(", ")")
        except ValueError:
            break
        if closing != len(result) - 1:
            break
        result = result[1:closing].strip()
    return result


def skip_csharp_trivia(text: str, start: int) -> int:
    """Skip whitespace and comments before a C# statement."""
    index = start
    while index < len(text):
        if text[index].isspace():
            index += 1
            continue
        if text.startswith("//", index):
            newline = text.find("\n", index + 2)
            return len(text) if newline < 0 else skip_csharp_trivia(text, newline + 1)
        if text.startswith("/*", index):
            closing = text.find("*/", index + 2)
            return len(text) if closing < 0 else skip_csharp_trivia(text, closing + 2)
        break
    return index


def branch_immediately_rejects(text: str, start: int) -> bool:
    """Return whether an if branch starts with return or throw."""
    index = skip_csharp_trivia(text, start)
    if index >= len(text):
        return False
    if text[index] == "{":
        index = skip_csharp_trivia(text, index + 1)
    return re.match(r"(?:return|throw)\b", text[index:]) is not None


def has_named_lookup(body: str, name: str) -> bool:
    """Detect a guarded dictionary lookup for one exact request field."""
    escaped = re.escape(name)
    return re.search(
        rf'\b(?:args|arguments|values)\s*\.\s*'
        rf'(?:TryGetValue|ContainsKey)\s*\(\s*"{escaped}"\s*[,)]',
        body) is not None


def missing_field_guard_rejects(body: str, name: str) -> bool:
    """Detect a missing-field guard that unconditionally rejects the request.

    A negated ``ContainsKey`` is evidence of a required field only when it is a
    complete top-level OR term in an ``if`` whose branch immediately returns or
    throws. This deliberately excludes defaulting, alternative-field, and
    compound guards.
    """
    escaped = re.escape(name)
    missing = re.compile(
        rf'!\s*(?:args|arguments|values)\s*\.\s*ContainsKey\s*'
        rf'\(\s*"{escaped}"\s*\)')
    for match in re.finditer(r"\bif\s*\(", body):
        opening = body.find("(", match.start())
        try:
            closing = matching(body, opening, "(", ")")
        except ValueError:
            continue
        if not branch_immediately_rejects(body, closing + 1):
            continue
        condition = strip_balanced_parentheses(body[opening + 1:closing])
        for term in split_top_level_operator(condition, "||"):
            if missing.fullmatch(strip_balanced_parentheses(term)):
                return True
    return False


def split_top_level_conditional(text: str) -> tuple[str, str] | None:
    """Return the two branches of a top-level C# conditional expression.

    A regex cannot distinguish a conditional inside a dictionary initializer from a
    conditional that selects the whole value. It also mistakes null-coalescing and
    null-conditional operators for ``?:``. Keep this scanner aligned with
    :func:`split_top_level` so nested initializers remain structurally inferable.
    """
    depths = {"(": 0, "[": 0, "{": 0}
    closing = {")": "(", "]": "[", "}": "{"}
    state = "code"
    question = -1
    nested_conditionals = 0
    index = 0
    while index < len(text):
        character = text[index]
        following = text[index + 1] if index + 1 < len(text) else ""
        previous = text[index - 1] if index > 0 else ""
        if state == "code":
            if character == '"':
                state = "string"
            elif character == "'":
                state = "char"
            elif character == "/" and following == "/":
                state = "line-comment"
                index += 1
            elif character == "/" and following == "*":
                state = "block-comment"
                index += 1
            elif character in depths:
                depths[character] += 1
            elif character in closing:
                depths[closing[character]] -= 1
            elif not any(depths.values()) and character == "?" and \
                    following not in {"?", ".", "["} and previous != "?":
                if question < 0:
                    question = index
                else:
                    nested_conditionals += 1
            elif not any(depths.values()) and character == ":" and question >= 0:
                if nested_conditionals:
                    nested_conditionals -= 1
                else:
                    return text[question + 1:index].strip(), text[index + 1:].strip()
        elif state in {"string", "char"}:
            if character == "\\":
                index += 1
            elif state == "string" and character == '"':
                state = "code"
            elif state == "char" and character == "'":
                state = "code"
        elif state == "line-comment":
            if character == "\n":
                state = "code"
        elif state == "block-comment" and character == "*" and following == "/":
            state = "code"
            index += 1
        index += 1
    return None


def has_top_level_boolean_operator(text: str) -> bool:
    """Detect boolean operators without inspecting lambda or call arguments."""
    depths = {"(": 0, "[": 0, "{": 0}
    closing = {")": "(", "]": "[", "}": "{"}
    state = "code"
    index = 0
    while index < len(text):
        character = text[index]
        following = text[index + 1] if index + 1 < len(text) else ""
        if state == "code":
            if character == '"':
                state = "string"
            elif character == "'":
                state = "char"
            elif character == "/" and following == "/":
                state = "line-comment"
                index += 1
            elif character == "/" and following == "*":
                state = "block-comment"
                index += 1
            elif character in depths:
                depths[character] += 1
            elif character in closing:
                depths[closing[character]] -= 1
            elif not any(depths.values()):
                pair = character + following
                if pair in {"==", "!=", ">=", "<=", "&&", "||"}:
                    return True
                if character in {"<", ">"} and following != character:
                    return True
                if character == "i" and re.match(
                        r"is\s+(?:not\s+)?(?:null|[A-Za-z_]\w*)\b", text[index:]):
                    previous = text[index - 1] if index > 0 else " "
                    if not (previous.isalnum() or previous == "_"):
                        return True
        elif state in {"string", "char"}:
            if character == "\\":
                index += 1
            elif state == "string" and character == '"':
                state = "code"
            elif state == "char" and character == "'":
                state = "code"
        elif state == "line-comment":
            if character == "\n":
                state = "code"
        elif state == "block-comment" and character == "*" and following == "/":
            state = "code"
            index += 1
        index += 1
    return False


def has_top_level_member(text: str, *members: str) -> bool:
    """Detect member access on the expression itself, not inside call arguments."""
    depths = {"(": 0, "[": 0, "{": 0}
    closing = {")": "(", "]": "[", "}": "{"}
    state = "code"
    index = 0
    while index < len(text):
        character = text[index]
        following = text[index + 1] if index + 1 < len(text) else ""
        if state == "code":
            if character == '"':
                state = "string"
            elif character == "'":
                state = "char"
            elif character == "/" and following == "/":
                state = "line-comment"
                index += 1
            elif character == "/" and following == "*":
                state = "block-comment"
                index += 1
            elif character in depths:
                depths[character] += 1
            elif character in closing:
                depths[closing[character]] -= 1
            elif character == "." and not any(depths.values()):
                if any(re.match(rf"\.{re.escape(member)}\b", text[index:])
                       for member in members):
                    return True
        elif state in {"string", "char"}:
            if character == "\\":
                index += 1
            elif state == "string" and character == '"':
                state = "code"
            elif state == "char" and character == "'":
                state = "code"
        elif state == "line-comment":
            if character == "\n":
                state = "code"
        elif state == "block-comment" and character == "*" and following == "/":
            state = "code"
            index += 1
        index += 1
    return False


def read_expression(text: str, start: int) -> tuple[str, int]:
    depths = {"(": 0, "[": 0, "{": 0}
    closing = {")": "(", "]": "[", "}": "{"}
    state = "code"
    index = start
    while index < len(text):
        character = text[index]
        following = text[index + 1] if index + 1 < len(text) else ""
        if state == "code":
            if character == '"':
                state = "string"
            elif character == "'":
                state = "char"
            elif character == "/" and following == "/":
                state = "line-comment"
                index += 1
            elif character == "/" and following == "*":
                state = "block-comment"
                index += 1
            elif character in depths:
                depths[character] += 1
            elif character in closing:
                depths[closing[character]] -= 1
            elif character == ";" and not any(depths.values()):
                return text[start:index].strip(), index
        elif state in {"string", "char"}:
            if character == "\\":
                index += 1
            elif state == "string" and character == '"':
                state = "code"
            elif state == "char" and character == "'":
                state = "code"
        elif state == "line-comment":
            if character == "\n":
                state = "code"
        elif state == "block-comment" and character == "*" and following == "/":
            state = "code"
            index += 1
        index += 1
    return text[start:].strip(), len(text)


def parse_dictionary(expression: str, start: int) -> tuple[list[tuple[str, str]], int]:
    opening = expression.find("{", start)
    closing = matching(expression, opening)
    body = expression[opening + 1:closing]
    entries: list[tuple[str, str]] = []
    index = 0
    while index < len(body):
        while index < len(body) and (body[index].isspace() or body[index] == ","):
            index += 1
        if index >= len(body) or body[index] != "{":
            break
        end = matching(body, index)
        parts = split_top_level(body[index + 1:end])
        if len(parts) >= 2:
            key = re.fullmatch(r'"((?:\\.|[^"])*)"', parts[0].strip())
            if key:
                entries.append((key.group(1), ",".join(parts[1:]).strip()))
        index = end + 1
    return entries, closing


def index_methods() -> dict[tuple[str, str], list[Method]]:
    methods: dict[tuple[str, str], list[Method]] = defaultdict(list)
    class_pattern = re.compile(
        r"\b(?:(?:internal|public|private|protected)\s+)?"
        r"(?:(?:static|sealed|abstract|partial)\s+)*class\s+(\w+)[^{]*\{")
    method_pattern = re.compile(
        r"\b(?:public|internal|private|protected)\s+(?:static\s+)?"
        r"([^\n{;=]+?)\s+(\w+)\s*\(([^)]*)\)\s*\{")
    for path in EDITOR.rglob("*.cs"):
        source = path.read_text(encoding="utf-8-sig")
        for class_match in class_pattern.finditer(source):
            class_open = source.find("{", class_match.end() - 1)
            try:
                class_close = matching(source, class_open)
            except ValueError:
                continue
            class_body = source[class_open + 1:class_close]
            for method_match in method_pattern.finditer(class_body):
                method_open = class_body.find("{", method_match.end() - 1)
                try:
                    method_close = matching(class_body, method_open)
                except ValueError:
                    continue
                method = Method(
                    class_match.group(1),
                    method_match.group(2),
                    " ".join(method_match.group(1).split()),
                    method_match.group(3),
                    class_body[method_open + 1:method_close],
                )
                methods[(method.class_name, method.name)].append(method)
    return methods


def parse_dispatch_handlers() -> dict[str, str]:
    if DESCRIPTOR_REGISTRY.exists():
        handlers: dict[str, str] = {}
        token = re.compile(r'\b(?:Create)?(?:Immediate|Deferred)\s*\(\s*"([^"]+)"\s*,')
        for source_path in (DESCRIPTOR_REGISTRY, LOCALIZATION_DESCRIPTOR_PROVIDER):
            if not source_path.exists():
                continue
            source = source_path.read_text(encoding="utf-8-sig")
            for match in token.finditer(source):
                opening = source.find("(", match.start())
                try:
                    closing = matching(source, opening, "(", ")")
                except ValueError:
                    continue
                arguments = split_top_level(source[opening + 1:closing])
                if len(arguments) >= 2:
                    handler = ",".join(arguments[1:]).strip()
                    handler = re.sub(r"[ \t]*\r?\n[ \t]*", "\n", handler)
                    lambda_match = re.match(
                        r"(?:arguments|_)\s*=>\s*(.*)", handler, re.S)
                    handlers[match.group(1)] = (
                        lambda_match.group(1).strip() if lambda_match else handler)
        if handlers:
            return handlers

    raise RuntimeError("The built-in route descriptor registry is required.")


def parse_registered_routes() -> list[str]:
    if DESCRIPTOR_REGISTRY.exists():
        routes: set[str] = set()
        for source_path in (DESCRIPTOR_REGISTRY, LOCALIZATION_DESCRIPTOR_PROVIDER):
            if source_path.exists():
                routes.update(re.findall(
                    r'\b(?:Create)?(?:Immediate|Deferred)\s*\(\s*"([^"]+)"\s*,',
                    source_path.read_text(encoding="utf-8-sig")))
        return sorted(routes)

    raise RuntimeError("The built-in route descriptor registry is required.")


def parse_deferred_routes() -> set[str]:
    if DESCRIPTOR_REGISTRY.exists():
        routes: set[str] = set()
        for source_path in (DESCRIPTOR_REGISTRY, LOCALIZATION_DESCRIPTOR_PROVIDER):
            if source_path.exists():
                routes.update(re.findall(
                    r'\b(?:Create)?Deferred\s*\(\s*"([^"]+)"\s*,',
                    source_path.read_text(encoding="utf-8-sig")))
        return routes
    raise RuntimeError("The built-in route descriptor registry is required.")


def method_for_handler(handler: str, methods: dict[tuple[str, str], list[Method]]) -> Method | None:
    match = re.search(r"\b(\w+)\.(\w+)\b", handler)
    if not match:
        return None
    candidates = methods.get((match.group(1), match.group(2)), [])
    if not candidates:
        return None
    return max(candidates, key=lambda candidate: len(candidate.body))


def return_expressions(method: Method) -> list[str]:
    ignored_spans: list[tuple[int, int]] = []
    nested_blocks = [
        re.compile(r"=>\s*\{"),
        re.compile(
            r"(?m)^\s*(?:void|bool|string|int|long|float|double|decimal|object|"
            r"[A-Z]\w*(?:<[^;={}]+>)?)\s+[A-Za-z_]\w*\s*\([^;{}]*\)\s*\{"),
        re.compile(r"\bdelegate\s*(?:\([^)]*\))?\s*\{"),
    ]
    for pattern in nested_blocks:
        for match in pattern.finditer(method.body):
            opening = method.body.find("{", match.start())
            try:
                closing = matching(method.body, opening)
            except ValueError:
                continue
            ignored_spans.append((opening, closing))

    result = []
    for match in re.finditer(r"\breturn\b", method.body):
        if any(start < match.start() < end for start, end in ignored_spans):
            continue
        expression, _ = read_expression(method.body, match.end())
        result.append(expression)
    for match in re.finditer(r"\bresolve\s*\(", method.body):
        opening = method.body.find("(", match.start())
        try:
            closing = matching(method.body, opening, "(", ")")
        except ValueError:
            continue
        result.append(method.body[opening + 1:closing].strip())
    return result


def local_types(method: Method) -> dict[str, str]:
    result: dict[str, str] = {}
    for parameter in split_top_level(method.parameters):
        match = re.match(
            r"\s*(?:(?:ref|out|in|this)\s+)?(.+?)\s+(\w+)\s*(?:=.*)?$",
            parameter, re.S)
        if match:
            result[match.group(2)] = " ".join(match.group(1).split())
    declaration = re.compile(
        r"\b(var|bool|string|int|long|float|double|decimal|[A-Za-z_]\w*(?:<[^;=]+>)?)\s+(\w+)\s*=\s*([^;]+);")
    for match in declaration.finditer(method.body):
        declared, name, initializer = match.groups()
        if declared != "var":
            result[name] = declared
        elif re.search(r"new\s+List<", initializer):
            generic = re.search(r"new\s+(List<.+>)\s*\(", initializer)
            if generic:
                result[name] = generic.group(1)
        elif re.search(r"new\s+Dictionary<", initializer):
            result[name] = "Dictionary<string, object>"
        elif initializer.strip().startswith(('"', '$"')):
            result[name] = "string"
        elif initializer.strip() in {"true", "false"}:
            result[name] = "bool"
    return result


def identifier_tokens(name: str) -> tuple[str, ...]:
    """Split a wire field name without treating arbitrary substrings as type hints."""
    normalized = re.sub(r"[^A-Za-z0-9]+", " ", name)
    pieces: list[str] = []
    for word in normalized.split():
        pieces.extend(re.findall(
            r"[A-Z]+(?=[A-Z][a-z]|\d|$)|[A-Z]?[a-z]+|\d+", word))
    return tuple(piece.lower() for piece in pieces)


def name_kind(name: str) -> str:
    lowered = name.lower()
    tokens = identifier_tokens(name)
    first = tokens[0] if tokens else lowered
    last = tokens[-1] if tokens else lowered

    # Exact names take precedence. In particular, ``issue`` and ``isolation`` are
    # not booleans, ``timestamp`` is not a numeric time value, and ``operation``
    # must not match the substring ``ratio``.
    if lowered in {
        "phase", "terminalstate", "component", "gameviewtitle", "blendparameter",
        "blendparametery", "scope", "layer", "newparent", "instance", "collection",
        "selectedlocale", "projectlocale", "group", "previouslocale", "category",
        "description", "projectroot", "collision", "noise", "testresult", "undostatus",
        "gameobject", "prefab", "renderpipeline", "base64", "sha256", "warning",
        "stacktrace", "filter", "graphmetadata", "preview", "windowcapture", "key",
        "folder", "reference", "action", "shape", "simulationspace", "template",
        "content", "field", "sprite", "alignment", "parent", "datafolder", "mainrole",
        "virtualrole", "top", "bottom", "left", "right", "stringparameter",
        "issue", "isolation", "timestamp", "operation", "operationkind", "kind",
        "captureflags", "rootnamespace", "userdata", "assetbundlevariant", "file",
        "method", "lockhash", "lockref", "manifestdependency", "manifestref", "prototype",
        "prototypetexture", "diffusetexture", "selector", "tooltip", "parameter",
    }:
        return "string"
    if lowered in {
        "valid", "enabled", "active", "visible", "exists", "success", "retryable",
        "dirty", "paused", "playing", "completed", "cancelled", "canceled", "truncated",
        "changed", "started", "orthographic", "mute", "solo", "loop", "mirror",
        "ikonfeet", "dryrun", "recoveredafterreload", "metapreserved", "compilationrequired",
        "recoveredfromsaveexception", "fileexists", "tempfileexists", "autostart",
        "deepprofiling", "profilerenabled", "orderedinterruption", "loopblend",
        "requiresexternaldebugger", "manageddebuggerattached", "scenedirty",
        "activeinhierarchy", "doublesidedgi", "enableinstancing", "receiveshadows",
        "carving", "drawgizmos", "querieshitbackfaces", "querieshittriggers",
        "defaultisfullscreen", "gpuskinning", "runinbackground", "includeinbuild",
        "drawheightmap", "drawinstanced", "drawtreesandfoliage", "alphaistransparency",
        "converttonormalmap", "readable", "srgb", "saved",
        "fromanystate", "keeporiginalorientation", "keeporiginalpositionxz",
        "keeporiginalpositiony", "loopblendorientation", "loopblendpositionxz",
        "loopblendpositiony", "playonawake", "pause", "activate", "local",
        "worldpositionstays", "carve", "ignore", "applyall", "revertall", "completely",
        "overwrite", "frameselected", "includechildren", "includeengine", "additive",
        "fill", "raycasttarget", "writedefaultvalues", "synchronizedspritenames",
        "cancelrequested", "timedout", "coloroverlifetime", "sizeoverlifetime",
        "velocityoverlifetime", "forceoverlifetime", "default", "editable", "preload",
        "loaded", "gameobjectactive", "manifestisgit", "enablealphadilation",
        "enablerotation", "enabletightpacking", "generatemipmaps", "enabledinhierarchy",
        "enabledself", "overrideglobalscenes", "trackstruncated", "continueonerror",
    } or (len(tokens) > 1 and first in {"is", "has", "can", "should", "was", "did"}) or \
            last in {"available", "enabled", "confirmed", "required", "preserved"}:
        return "boolean"
    if lowered in {
        "actionid", "requestid", "processid", "frameid", "ticketid", "instanceid",
        "nextoffset", "activeport", "registeredport", "currentport", "firstframe",
        "lastframe", "heightmapresolution", "renderqueue", "sortingorder", "layer1",
        "layer2", "antialiasing", "maximumlodlevel", "particleraycastbudget", "level",
        "alphamapresolution", "basemapresolution", "detailresolution", "totaltreeinstances",
        "nextresultoffset", "resultlimit", "resultoffset", "totaltiles", "anisolevel",
        "compressionquality", "intparameter", "resolution", "maxevents", "maxparticles",
        "maxresults", "octaves", "lodlevels", "undogroup", "localfileid",
        "defaultsolveriterations", "defaultsolvervelocityiterations", "texturewidth",
        "textureheight", "maxtexturesize", "fontsize", "maxnesteddepth",
        "maxstringlength", "cullingmask", "arraysize", "filesize", "maxdimension",
        "padding", "operationsperframe", "framebudgetms", "timeoutms",
    } or last in {"count", "index", "offset"} or lowered.endswith("hash") or lowered in {
        "count", "offset", "limit", "line", "column", "port", "width", "height",
        "depth", "frame", "frames", "rows", "columns", "attempt", "attempts",
    } or (first in {"total", "returned", "remaining", "max", "min"} and
          last in {
              "actions", "assets", "entries", "events", "instances", "items", "jobs",
              "keyframes", "layers", "lights", "maps", "matches", "nodes", "objects",
              "packages", "parameters", "references", "results", "scenes", "tests",
              "tools", "transitions", "warnings",
          }):
        return "integer"
    if lowered in {
        "framerate", "length", "fogdensity", "globalvolume", "volume", "intensity",
        "ambientintensity", "averagedelta", "gap", "overlap", "framefps", "framegpums",
        "frametotalms", "gfxdrivermb", "monoheapmb", "monousedmb", "tempallocatormb",
        "totalallocatedmb", "totalreservedmb", "totalunusedreservedmb", "scannedassettotalmb",
        "totalscannedmb", "defaultcontactoffset", "lodbias", "pixelsperunit", "worldy",
        "spritepixelsperunit", "requestedzoom", "elapsedms", "fraction", "cycleoffset",
        "floatparameter", "pitch", "spatialblend", "range", "agentclimb", "agentheight",
        "agentslope", "gravitymodifier", "startrotation", "positionx", "positiony",
        "bendfactor", "maxheight", "maxwidth", "minheight", "minwidth", "maxx", "maxz",
        "minx", "minz", "tileheight", "tilelength", "tilewidth", "worldx", "worldz",
        "step", "amplitude", "baseheight", "lacunarity", "offsetx", "offsetz", "persistence",
        "density", "strength", "maxaltitude", "minaltitude", "maxsteepness", "minsteepness",
        "seed", "delta", "falloff", "coverage", "detailobjectdensity",
        "detailresolutionperpatch", "treecrossfadelength", "arc",
        "acceleration", "fixedtimestep", "realtimesincestartup",
        "xmin", "ymin", "xmax", "ymax", "bounceintensity", "shadowstrength",
        "screenrelativetransitionheight", "climb", "slope", "rangedefault", "rangemin",
        "rangemax", "flexgrow", "flexshrink", "opacity", "marginbottom", "marginleft",
        "marginright", "margintop", "paddingbottom", "paddingleft", "paddingright",
        "paddingtop",
    } or lowered in {"x", "y", "z", "w", "r", "g", "b", "a"} or any(
        token in {
            "duration", "threshold", "time", "scale", "weight", "speed", "distance",
            "radius", "angle", "memory", "progress", "value", "tangent", "seconds",
            "milliseconds", "percent", "ratio", "fps",
        }
        for token in tokens
    ):
        return "number"
    if last in {"id", "token"} or lowered in {
        "idempotencykey", "jobid", "testjobid", "transactionid",
    }:
        return "string"
    if lowered in {
        "results", "operations", "entries", "issues", "assets", "lights", "matches",
        "colliders", "nodes", "instances", "jobs", "collections", "packages", "players",
        "hits", "items", "categories", "terrains", "tests", "tracks", "elements",
        "checks", "children", "audits", "documents", "actions", "connections", "shaders",
        "graphs", "edges", "errors", "warnings", "effects", "groups", "snapshots",
        "components", "constraints", "events", "sprites", "transitions", "created",
        "references", "imports", "moves", "subassets", "maps", "scenes", "hierarchy",
        "exposedparameters", "exposedproperties", "startuplocaleselectors", "createdtables",
        "availablejobs", "folders", "extensions", "labels", "statenames", "sideeffects",
        "requestedkeywords", "truncatedassets",
        "schematypes", "platforms", "missing", "keywords", "classes", "comparisons",
    }:
        return "array"
    if any(token in {
        "name", "path", "type", "status", "mode", "guid", "message", "error", "code",
        "hash", "token", "route", "source", "destination", "platform", "version", "label",
        "text", "shader", "tag", "property", "format", "reason", "query", "url", "locale",
        "identifier", "utc", "exception", "title", "role",
    } for token in tokens):
        return "string"
    return "json"


def merge_field_shapes(shapes: list[tuple[Field, ...]]) -> tuple[Field, ...]:
    if not shapes:
        return ()
    by_name: dict[str, list[Field]] = defaultdict(list)
    for shape in shapes:
        for field in shape:
            by_name[field.name].append(field)
    merged: list[Field] = []
    for name, candidates in by_name.items():
        first = candidates[0]
        same_shape = all(
            (candidate.kind, candidate.item_kind, candidate.fields, candidate.nullable) ==
            (first.kind, first.item_kind, first.fields, first.nullable)
            for candidate in candidates[1:]
        )
        merged.append(dataclasses.replace(
            first if same_shape else Field(name, "json"),
            required=len(candidates) == len(shapes) and all(
                candidate.required for candidate in candidates)))
    return tuple(merged)


def expression_item_shapes(expression: str, method: Method,
                           methods: dict[tuple[str, str], list[Method]],
                           active: set[str]) -> list[tuple[Field, ...]]:
    shapes: list[tuple[Field, ...]] = []
    dictionary_match = re.search(r"new\s+Dictionary<string,\s*object>\s*\{", expression)
    if dictionary_match:
        entries, _ = parse_dictionary(expression, dictionary_match.start())
        shapes.append(tuple(infer_expression(
            key, item, method, methods, True) for key, item in entries))

    selector = re.search(
        r"\.Select\s*\(\s*(?:(?:\w+)\s*=>\s*)?(?:(\w+)\.)?(\w+)\s*(?:\(|\))",
        expression)
    if selector:
        class_name = selector.group(1) or method.class_name
        candidates = methods.get((class_name, selector.group(2)), [])
        if candidates:
            shapes.extend(output_shapes(
                max(candidates, key=lambda item: len(item.body)), methods,
                {(method.class_name, method.name)}))

    call = re.fullmatch(r"(?:(\w+)\.)?(\w+)\s*\(.*\)", expression.strip(), re.S)
    if call:
        class_name = call.group(1) or method.class_name
        candidates = methods.get((class_name, call.group(2)), [])
        if candidates:
            shapes.extend(output_shapes(
                max(candidates, key=lambda item: len(item.body)), methods,
                {(method.class_name, method.name)}))
    return shapes


def infer_list_item_fields(method: Method, variable: str,
                           methods: dict[tuple[str, str], list[Method]],
                           active: set[str] | None = None) -> tuple[Field, ...]:
    active = set(active or ())
    if variable in active:
        return ()
    active.add(variable)
    shapes: list[tuple[Field, ...]] = []

    for operation in ("Add", "AddRange"):
        for addition in re.finditer(
                rf"\b{re.escape(variable)}\.{operation}\s*\(", method.body):
            opening = method.body.find("(", addition.start())
            try:
                closing = matching(method.body, opening, "(", ")")
            except ValueError:
                continue
            argument = method.body[opening + 1:closing].strip()
            argument_variable = re.fullmatch(r"(\w+)", argument)
            if argument_variable:
                nested_variable = argument_variable.group(1)
                resolved = resolve_variable(method, nested_variable)
                if resolved:
                    shapes.extend(expression_item_shapes(
                        resolved, method, methods, active))
                nested = infer_list_item_fields(method, nested_variable, methods, active)
                if nested:
                    shapes.append(nested)
            else:
                shapes.extend(expression_item_shapes(argument, method, methods, active))

    initializer = resolve_variable(method, variable) or ""
    if initializer:
        shapes.extend(expression_item_shapes(initializer, method, methods, active))
        upstream = re.match(r"(\w+)\.(?:Skip|Take|Where|OrderBy|OrderByDescending|ToList)",
                            initializer)
        if upstream:
            nested = infer_list_item_fields(method, upstream.group(1), methods, active)
            if nested:
                shapes.append(nested)

    return merge_field_shapes(shapes)


def infer_expression(name: str, expression: str, method: Method,
                     methods: dict[tuple[str, str], list[Method]],
                     required: bool = False) -> Field:
    value = expression.strip()
    nullable = bool(re.search(r"\bnull\b", value))
    conditional = split_top_level_conditional(value)
    if conditional:
        candidates = [candidate.strip() for candidate in conditional
                      if candidate.strip() != "null"]
        if candidates:
            inferred_candidates = [
                infer_expression(name, candidate, method, methods, required)
                for candidate in candidates
            ]
            first = inferred_candidates[0]
            if all(candidate.kind == first.kind and
                   candidate.item_kind == first.item_kind
                   for candidate in inferred_candidates[1:]):
                return dataclasses.replace(
                    first, nullable=nullable or first.nullable, required=required)
            return Field(name, "json", required=required, nullable=nullable)
    dictionary = re.match(r"new\s+Dictionary<string,\s*object>\s*\{", value)
    if dictionary:
        entries, _ = parse_dictionary(value, dictionary.start())
        fields = tuple(infer_expression(key, item, method, methods, True) for key, item in entries)
        return Field(name, "object", required=required, fields=fields, nullable=nullable)
    anonymous = re.match(r"new\s*\{(.*)\}\s*$", value, re.S)
    if anonymous:
        fields = []
        for item in split_top_level(anonymous.group(1)):
            assignment = re.match(r"(\w+)\s*=\s*(.*)", item, re.S)
            if assignment:
                fields.append(infer_expression(
                    assignment.group(1), assignment.group(2), method, methods, True))
        return Field(name, "object", required=required, fields=tuple(fields), nullable=nullable)
    code_value = re.sub(r'(?s)@?\$?"(?:\\.|""|[^"])*"', '""', value)
    if re.fullmatch(r"(?:true|false)", code_value) or \
            has_top_level_boolean_operator(code_value) or re.fullmatch(
            r"!?\s*.+\.(?:Contains|Any|All)\(.*\)", code_value, re.S):
        return Field(name, "boolean", required=required, nullable=nullable)
    if value.startswith(('"', '$"')) or has_top_level_member(
            value, "ToString") or value.endswith(".name"):
        return Field(name, "string", required=required, nullable=nullable)
    if re.fullmatch(r"[-+]?\d+[lLuU]*", value) or has_top_level_member(
            value, "Count", "Length"):
        return Field(name, "integer", required=required, nullable=nullable)
    if re.fullmatch(r"[-+]?(?:\d+\.\d*|\.\d+)(?:[fFdDmM])?", value):
        return Field(name, "number", required=required, nullable=nullable)
    types = local_types(method)
    simple = re.fullmatch(r"(\w+)(?:\.ToArray\(\)|\.ToList\(\))?", value)
    if simple:
        variable = simple.group(1)
        declared = types.get(variable, "")
        if declared in {"bool"}:
            return Field(name, "boolean", required=required, nullable=nullable)
        if declared in {"string"}:
            return Field(name, "string", required=required, nullable=nullable)
        if declared in {"int", "long"}:
            return Field(name, "integer", required=required, nullable=nullable)
        if declared in {"float", "double", "decimal"}:
            return Field(name, "number", required=required, nullable=nullable)
        if declared == "Dictionary<string, object>":
            resolved = resolve_variable(method, variable)
            if resolved and resolved != value:
                inferred = infer_expression(name, resolved, method, methods, required)
                return dataclasses.replace(inferred, required=required,
                                           nullable=nullable or inferred.nullable)
        list_type = re.search(r"List<(.+)>", declared)
        if list_type:
            item_type = list_type.group(1).strip()
            item_kind = {
                "string": "string", "int": "integer", "long": "integer",
                "float": "number", "double": "number", "bool": "boolean",
            }.get(item_type, "json")
            nested = list(infer_list_item_fields(method, variable, methods))
            if nested:
                item_kind = "object"
            for addition in ([] if nested else re.finditer(
                    rf"\b{re.escape(variable)}\.Add\s*\(", method.body)):
                opening = method.body.find("(", addition.start())
                try:
                    closing = matching(method.body, opening, "(", ")")
                except ValueError:
                    continue
                argument = method.body[opening + 1:closing].strip()
                argument_variable = re.fullmatch(r"(\w+)", argument)
                if argument_variable:
                    variable_name = argument_variable.group(1)
                    argument = resolve_variable(method, variable_name) or argument
                    dictionary_match = re.search(
                        r"new\s+Dictionary<string,\s*object>\s*\{", argument)
                    if dictionary_match:
                        entries, _ = parse_dictionary(argument, dictionary_match.start())
                        nested = [infer_expression(key, item, method, methods, True)
                                  for key, item in entries]
                        for assignment_name, assignment_value in dictionary_assignments(
                                method.body, variable_name):
                            nested.append(infer_expression(
                                assignment_name, assignment_value,
                                method, methods, False))
                    else:
                        nested = []
                else:
                    dictionary_match = re.search(
                        r"new\s+Dictionary<string,\s*object>\s*\{", argument)
                    if dictionary_match:
                        entries, _ = parse_dictionary(argument, dictionary_match.start())
                        nested = [infer_expression(key, item, method, methods, True)
                                  for key, item in entries]
                    else:
                        call_match = re.match(r"(?:(\w+)\.)?(\w+)\s*\(", argument)
                        call_class = call_match.group(1) if call_match else None
                        call_name = call_match.group(2) if call_match else None
                        candidates = methods.get((call_class or method.class_name, call_name), []) \
                            if call_name else []
                        candidate_shapes = output_shapes(
                            max(candidates, key=lambda item: len(item.body)), methods,
                            {(method.class_name, method.name)}) if candidates else []
                        nested = list(candidate_shapes[0]) if len(candidate_shapes) == 1 else []
                if nested:
                    nested = list({field.name: field for field in nested}.values())
                    item_kind = "object"
                    break
            if not nested:
                initializer = resolve_variable(method, variable) or ""
                selector = re.search(r"\.Select\s*\(\s*(?:(?:\w+)\s*=>\s*)?(\w+)\s*(?:\(|\))",
                                     initializer)
                if selector:
                    candidates = methods.get((method.class_name, selector.group(1)), [])
                    candidate_shapes = output_shapes(
                        max(candidates, key=lambda item: len(item.body)), methods,
                        {(method.class_name, method.name)}) if candidates else []
                    if len(candidate_shapes) == 1:
                        nested = list(candidate_shapes[0])
                        item_kind = "object"
            return Field(name, "array", required=required, item_kind=item_kind,
                         fields=tuple(nested), nullable=nullable)
        resolved = resolve_variable(method, variable)
        if resolved and resolved != value:
            inferred = infer_expression(name, resolved, method, methods, required)
            return dataclasses.replace(inferred, required=required,
                                       nullable=nullable or inferred.nullable)
    call = re.match(r"(?:(\w+)\.)?(\w+)\s*\(", value)
    if call:
        receiver = call.group(1)
        receiver_type = local_types(method).get(receiver, "") if receiver else ""
        class_name = receiver_type.rsplit(".", 1)[-1] if receiver_type else \
            (receiver or method.class_name)
        if class_name == "VmObjectId" and call.group(2) == "Get":
            return Field(name, "string", required=required, nullable=nullable)
        candidates = methods.get((class_name, call.group(2)), [])
        if candidates:
            shapes = output_shapes(max(candidates, key=lambda item: len(item.body)), methods)
            if len(shapes) == 1:
                return Field(name, "object", required=required,
                             fields=shapes[0], nullable=nullable)
    if re.search(r"(?:ToArray|ToList|Select|Cast|new\s+(?:List|\[))", value):
        return Field(name, "array", required=required, item_kind="json", nullable=nullable)
    guessed = name_kind(name)
    string_arrays = {
        "labels", "stateNames", "excludePlatforms", "includePlatforms",
        "completionConfirmedBy", "newAssetPaths", "synchronizedSpriteNames",
        "globalScenes", "enabledKeywords", "requestedKeywords", "captureFlags",
        "extensions", "folders", "truncatedAssets", "sideEffects", "schemaTypes",
        "platforms", "missing", "keywords", "classes",
    }
    return Field(name, guessed, required=required,
                 item_kind=("string" if name in string_arrays else "json")
                 if guessed == "array" else None,
                 nullable=nullable)


def resolve_variable(method: Method, variable: str) -> str | None:
    declaration = re.compile(
        rf"\b(?:var|object|Dictionary<string,\s*object>)\s+{re.escape(variable)}\s*=\s*")
    matches = list(declaration.finditer(method.body))
    if not matches:
        return None
    expression, _ = read_expression(method.body, matches[-1].end())
    return expression


def dictionary_assignments(body: str, variable: str) -> list[tuple[str, str]]:
    pattern = re.compile(
        rf"\b{re.escape(variable)}\s*\[\s*\"([^\"]+)\"\s*\]\s*=\s*")
    assignments: list[tuple[str, str]] = []
    for match in pattern.finditer(body):
        expression, _ = read_expression(body, match.end())
        assignments.append((match.group(1), expression))
    return assignments


def output_shapes(method: Method, methods: dict[tuple[str, str], list[Method]],
                  active: set[tuple[str, str]] | None = None) -> list[tuple[Field, ...]]:
    active = set(active or ())
    identity = (method.class_name, method.name)
    if identity in active:
        return []
    active.add(identity)
    shapes: list[tuple[Field, ...]] = []
    for expression in return_expressions(method):
        if "VmAutomationResponse.Error" in expression or re.search(r"\berror\s*=", expression):
            continue
        unwrapped = expression
        wrapper = re.match(r"(?:VmAutomationResponse\.(?:Success|Ok)|Task\.FromResult)\s*\((.*)\)\s*$",
                           unwrapped, re.S)
        if wrapper:
            unwrapped = wrapper.group(1)
        variable = re.fullmatch(r"(\w+)", unwrapped)
        returned_variable = variable.group(1) if variable else None
        if variable:
            resolved = resolve_variable(method, returned_variable)
            if resolved:
                unwrapped = resolved
        dictionary = re.search(r"new\s+Dictionary<string,\s*object>\s*\{", unwrapped)
        if dictionary:
            entries, end = parse_dictionary(unwrapped, dictionary.start())
            entry_values = dict(entries)
            if "error" in entry_values and (
                    "errorCode" in entry_values or entry_values.get("success", "").strip() == "false" or
                    set(entry_values).issubset({"success", "error", "message", "errorCode", "retryable"})):
                continue
            fields = [infer_expression(key, value, method, methods, True)
                      for key, value in entries]
            if returned_variable:
                tail = method.body
                for assignment_name, assignment_value in dictionary_assignments(
                        tail, returned_variable):
                    fields.append(infer_expression(
                        assignment_name, assignment_value,
                        method, methods, False))
            fields = [field for field in fields if field.name != "success"]
            if fields:
                shapes.append(tuple(dict.fromkeys(fields)))
            continue
        anonymous = re.search(r"new\s*\{(.*)\}", unwrapped, re.S)
        if anonymous:
            fields = []
            for item in split_top_level(anonymous.group(1)):
                assignment = re.match(r"(\w+)\s*=\s*(.*)", item, re.S)
                if assignment and assignment.group(1) != "success":
                    fields.append(infer_expression(
                        assignment.group(1), assignment.group(2), method, methods, True))
            if fields:
                shapes.append(tuple(fields))
            continue
        call = re.match(r"(?:(\w+)\.)?(\w+)\s*\(", unwrapped)
        if call:
            class_name = call.group(1) or method.class_name
            candidates = methods.get((class_name, call.group(2)), [])
            if candidates:
                called_shapes = output_shapes(
                    max(candidates, key=lambda item: len(item.body)), methods, active)
                assignments: list[Field] = []
                if returned_variable:
                    for assignment_name, assignment_value in dictionary_assignments(
                            method.body, returned_variable):
                        assignments.append(infer_expression(
                            assignment_name, assignment_value,
                            method, methods, False))
                if assignments:
                    for called_shape in called_shapes:
                        shapes.append(tuple(dict.fromkeys(called_shape + tuple(assignments))))
                else:
                    shapes.extend(called_shapes)
    unique: dict[tuple[str, ...], tuple[Field, ...]] = {}
    for shape in shapes:
        unique.setdefault(tuple(field.name for field in shape), shape)
    return list(unique.values())


def input_fields(method: Method) -> tuple[Field, ...]:
    fields: dict[str, Field] = {}
    patterns = [
        (r"\b(Get|TryGet|Require)(String|Bool|Boolean|Int|Integer|Long|Float|Double|Dictionary|List|StringList|IntList|Vector2|Vector3|Vector4|Quaternion|Color|Rect)\w*\s*\([^,]+,\s*\"([^\"]+)\"", None),
        (r"\b(?:args|arguments|values)\s*\.\s*(?:TryGetValue|ContainsKey)\s*\(\s*\"([^\"]+)\"", "lookup"),
        (r"\b(?:args|arguments|values)\s*\[\s*\"([^\"]+)\"\s*\]", "direct"),
    ]
    kinds = {
        "String": "string", "Bool": "boolean", "Boolean": "boolean",
        "Int": "integer", "Integer": "integer", "Long": "integer",
        "Float": "number", "Double": "number", "Dictionary": "object",
        "List": "array", "StringList": "array", "IntList": "array",
        "Vector2": "object", "Vector3": "object", "Vector4": "object",
        "Quaternion": "object", "Color": "object", "Rect": "object",
    }
    for pattern, fixed in patterns:
        for match in re.finditer(pattern, method.body):
            if fixed is None:
                access_kind, accessor, name = match.groups()
                kind = kinds.get(accessor, "json")
                item_kind = "string" if accessor == "StringList" else (
                    "integer" if accessor == "IntList" else "json")
                component_names = {
                    "Vector2": ("x", "y"),
                    "Vector3": ("x", "y", "z"),
                    "Vector4": ("x", "y", "z", "w"),
                    "Quaternion": ("x", "y", "z", "w"),
                    "Color": ("r", "g", "b", "a"),
                    "Rect": ("x", "y", "width", "height"),
                }.get(accessor)
                nested = tuple(Field(component, "number") for component in component_names or ())
            else:
                access_kind = fixed
                name = match.group(1)
                kind = name_kind(name)
                item_kind = "json" if kind == "array" else None
                nested = ()
                vector_components = {
                    "position": ("x", "y", "z"),
                    "rotation": ("x", "y", "z"),
                    "lookAt": ("x", "y", "z"),
                    "pivot": ("x", "y", "z"),
                    "gravity": ("x", "y", "z"),
                    "anchoredPosition": ("x", "y"),
                    "color": ("r", "g", "b", "a"),
                    "ambientColor": ("r", "g", "b", "a"),
                    "fogColor": ("r", "g", "b", "a"),
                    "dryColor": ("r", "g", "b", "a"),
                    "healthyColor": ("r", "g", "b", "a"),
                }.get(name)
                if vector_components:
                    kind = "object"
                    nested = tuple(Field(component, "number") for component in vector_components)
            required = access_kind == "Require" or missing_field_guard_rejects(
                method.body, name)
            if access_kind == "direct" and not has_named_lookup(method.body, name):
                required = True
            candidate = Field(name, kind, required=required,
                              item_kind=item_kind, fields=nested)
            if name not in fields or (required and not fields[name].required):
                fields[name] = candidate
    return tuple(sorted(fields.values(), key=lambda field: field.name))


def field_to_json(field: Field) -> dict[str, object]:
    if field.name == "rootWorldBound":
        return NULLABLE_UI_RECT
    result: dict[str, object]
    if field.kind == "array":
        item: dict[str, object]
        if field.item_kind == "object" and field.fields:
            item = {
                "type": "object",
                "properties": {nested.name: field_to_json(nested) for nested in field.fields},
                "additionalProperties": False,
            }
            required = [nested.name for nested in field.fields if nested.required]
            if required:
                item["required"] = required
        elif field.item_kind and field.item_kind != "json":
            item = {"type": field.item_kind}
        else:
            item = {"$ref": "#/$defs/unityJsonValue"}
        result = {"type": "array", "items": item}
    elif field.kind == "object":
        if field.fields:
            result = {
                "type": "object",
                "properties": {nested.name: field_to_json(nested) for nested in field.fields},
                "additionalProperties": False,
            }
            required = [nested.name for nested in field.fields if nested.required]
            if required:
                result["required"] = required
        else:
            result = {
                "type": "object",
                "additionalProperties": {"$ref": "#/$defs/unityJsonValue"},
            }
    elif field.kind == "json":
        result = {"$ref": "#/$defs/unityJsonValue"}
    else:
        result = {"type": field.kind}
    if field.nullable and "type" in result and isinstance(result["type"], str):
        result["type"] = [result["type"], "null"]
    return result


def describe_schema_properties(schema: dict[str, object], route: str,
                               direction: str) -> dict[str, object]:
    """Attach stable descriptions to every published object property.

    Exact schemas are generated from the handler source, so a property description must
    identify both its wire name and the route that owns its meaning. Route-specific hand
    authored schemas keep their richer descriptions; this function only fills omissions.
    """
    described = copy.deepcopy(schema)

    def visit(node: object) -> None:
        if not isinstance(node, dict):
            return
        properties = node.get("properties")
        if isinstance(properties, dict):
            for name, child in properties.items():
                if isinstance(child, dict):
                    child = copy.deepcopy(child)
                    properties[name] = child
                    child.setdefault(
                        "description",
                        f"`{name}` {direction} field for `{route}`.")
                    visit(child)
        items = node.get("items")
        if isinstance(items, dict):
            items = copy.deepcopy(items)
            node["items"] = items
            visit(items)
        for keyword in ("allOf", "anyOf", "oneOf"):
            variants = node.get(keyword)
            if isinstance(variants, list):
                variants = copy.deepcopy(variants)
                node[keyword] = variants
                for variant in variants:
                    visit(variant)

    visit(described)
    return described


def unresolved_field_paths(route: str, shapes: list[tuple[Field, ...]]) -> list[str]:
    """Find opaque inferred leaves at any depth unless a reviewed override owns them."""
    unresolved: set[str] = set()

    def visit(field: Field, path: str, top_level: bool) -> None:
        if top_level and (route, field.name) in OUTPUT_PROPERTY_OVERRIDES:
            return
        current = f"{path}.{field.name}" if path else field.name
        if field.kind == "json":
            unresolved.add(current)
            return
        if field.kind == "array" and field.item_kind == "json":
            unresolved.add(current + "[]")
        for child in field.fields:
            visit(child, current + ("[]" if field.kind == "array" else ""), False)

    for shape in shapes:
        for field in shape:
            visit(field, "", True)
    return sorted(unresolved)


def audit() -> dict[str, object]:
    methods = index_methods()
    handlers = parse_dispatch_handlers()
    registered = parse_registered_routes()
    explicit_input_sources = (
        EDITOR / "VmAutomationToolInputSchemaCatalog.cs",
        EDITOR / "VmAutomationSpecializedToolInputSchemaCatalog.cs",
    )
    explicit_inputs = {
        route
        for source_path in explicit_input_sources
        for route in re.findall(
            r'case\s+"([^"]+)"\s*:',
            source_path.read_text(encoding="utf-8-sig"))
    }
    selector_routes = {
        route for route, handler in handlers.items()
        if (method := method_for_handler(handler, methods)) is not None and
        re.search(r"\b(?:VmAutomationGameObjectCommands\.)?FindGameObject\s*\(\s*args\s*\)",
                  method.body)
    }
    classified_selector_routes = set(SCENE_GAME_OBJECT_SELECTOR_REQUIREMENTS)
    if selector_routes != classified_selector_routes:
        missing = sorted(selector_routes - classified_selector_routes)
        stale = sorted(classified_selector_routes - selector_routes)
        raise RuntimeError(
            "Scene GameObject selector classification is out of date. "
            f"Missing routes: {missing}; stale routes: {stale}.")
    routes = []
    for route in registered:
        handler = handlers.get(route, "")
        method = method_for_handler(handler, methods)
        shapes = output_shapes(method, methods) if method else []
        inputs = () if route in explicit_inputs or not method else input_fields(method)
        generated_input = None
        if route not in explicit_inputs:
            input_properties = {field.name: field_to_json(field) for field in inputs}
            if route in SCENE_GAME_OBJECT_SELECTOR_REQUIREMENTS:
                input_properties.setdefault("path", copy.deepcopy(STRING))
                input_properties.setdefault("instanceId", copy.deepcopy(STRING))
            for (override_route, field_name), schema in INPUT_PROPERTY_OVERRIDES.items():
                if override_route == route:
                    if field_name not in input_properties:
                        raise RuntimeError(
                            f"Input override {route}.{field_name} has no extracted field.")
                    input_properties[field_name] = schema
            required_inputs = list(dict.fromkeys(
                [field.name for field in inputs if field.required] +
                list(INPUT_REQUIRED_OVERRIDES.get(route, ()))
            ))
            generated_input = {
                "type": "object",
                "properties": input_properties,
                "additionalProperties": False,
                **({"required": required_inputs} if required_inputs else {}),
            }
            selector_requirements = SCENE_GAME_OBJECT_SELECTOR_REQUIREMENTS.get(route, ())
            if selector_requirements:
                generated_input["anyOf"] = [
                    {"required": [field_name]}
                    for field_name in selector_requirements
                ]
            generated_input = describe_schema_properties(
                generated_input, route, "request")
        unresolved = unresolved_field_paths(route, shapes)
        output_schemas = OUTPUT_SCHEMA_OVERRIDES.get(route)
        if output_schemas is not None:
            unresolved = []
        if output_schemas is None:
            output_schemas = [
                {
                    "type": "object",
                    "properties": {field.name: field_to_json(field) for field in shape},
                    "additionalProperties": False,
                    **({"required": [field.name for field in shape if field.required]}
                       if any(field.required for field in shape) else {}),
                }
                for shape in shapes
            ]
            for schema in output_schemas:
                properties = schema["properties"]
                for (override_route, field_name), property_schema in \
                        OUTPUT_PROPERTY_OVERRIDES.items():
                    if override_route == route and field_name in properties:
                        properties[field_name] = property_schema
        if route in NON_CATALOG_ROUTES:
            unresolved = []
        output_schemas = [
            describe_schema_properties(schema, route, "response")
            for schema in output_schemas
        ]
        routes.append({
            "route": route,
            "handler": handler,
            "handlerResolved": method is not None,
            "inputSource": "explicit" if route in explicit_inputs else "generated",
            "generatedInput": generated_input,
            "outputShapes": output_schemas,
            "unresolvedOutputFields": unresolved,
        })
    return {
        "registeredRouteCount": len(registered),
        "handlerResolvedCount": sum(1 for route in routes if route["handlerResolved"]),
        "outputResolvedCount": sum(1 for route in routes
                                   if route["route"] in NON_CATALOG_ROUTES or
                                   route["outputShapes"]),
        "generatedInputCount": sum(1 for route in routes if route["inputSource"] == "generated"),
        "unresolvedOutputRouteCount": sum(1 for route in routes
                                          if route["route"] not in NON_CATALOG_ROUTES and
                                          route["unresolvedOutputFields"]),
        "routes": routes,
    }


def csharp_identifier(prefix: str, route: str) -> str:
    words = re.split(r"[^A-Za-z0-9]+", route)
    return prefix + "_" + "_".join(word for word in words if word)


def render_schema(schema: dict[str, object], indent: str = "            ",
                  require_all: bool = False, allow_helpers: bool = True) -> str:
    if isinstance(schema.get("description"), str):
        description = schema["description"]
        undecorated = dict(schema)
        undecorated.pop("description")
        rendered = render_schema(
            undecorated, indent, require_all, allow_helpers)
        return f"Describe({rendered}, {json.dumps(description)})"
    constraint_names = ("minimum", "maximum", "minItems", "uniqueItems")
    constraints = {name: schema[name] for name in constraint_names if name in schema}
    if constraints:
        unconstrained = dict(schema)
        for name in constraints:
            unconstrained.pop(name)
        rendered = render_schema(
            unconstrained, indent, require_all, allow_helpers)
        arguments = []
        for name in constraint_names:
            if name not in constraints:
                continue
            value = constraints[name]
            arguments.append(f"{name}: {str(value).lower() if isinstance(value, bool) else value}")
        return f"Constrain({rendered}, " + ", ".join(arguments) + ")"
    any_of = schema.get("anyOf")
    if schema.get("type") == "object" and isinstance(any_of, list) and all(
            isinstance(variant, dict) and set(variant) == {"required"} and
            isinstance(variant["required"], list) and len(variant["required"]) == 1 and
            isinstance(variant["required"][0], str)
            for variant in any_of):
        unconstrained = dict(schema)
        unconstrained.pop("anyOf")
        rendered = render_schema(
            unconstrained, indent, require_all, allow_helpers)
        alternatives = ", ".join(
            json.dumps(variant["required"][0]) for variant in any_of)
        return f"RequireAnyOf({rendered}, {alternatives})"
    if allow_helpers:
        for helper_name, helper_schema in REUSABLE_OUTPUT_SCHEMAS.items():
            if schema == helper_schema:
                return f"{helper_name}()"
    if "$ref" in schema:
        return "JsonValue()"
    if "oneOf" in schema:
        variants = [
            render_schema(variant, indent + "    ", require_all, allow_helpers)
            for variant in schema["oneOf"]
        ]
        return "OneOf(\n" + indent + "    " + \
            (",\n" + indent + "    ").join(variants) + ")"
    enum_values = schema.get("enum")
    if isinstance(enum_values, list) and enum_values and \
            all(isinstance(value, str) for value in enum_values):
        return "Enum(" + ", ".join(json.dumps(value) for value in enum_values) + ")"
    schema_type = schema.get("type")
    if isinstance(schema_type, list):
        concrete = next((item for item in schema_type if item != "null"), "string")
        if len(schema_type) == 2 and "null" in schema_type and \
                concrete not in {"array", "object"}:
            return f'Nullable("{concrete}")'
        variants = []
        for variant_type in schema_type:
            variant = dict(schema)
            variant["type"] = variant_type
            variants.append(render_schema(
                variant, indent + "    ", require_all, allow_helpers))
        return "OneOf(\n" + indent + "    " + \
            (",\n" + indent + "    ").join(variants) + ")"
    if schema_type == "array":
        return f"Array({render_schema(schema['items'], indent, require_all, allow_helpers)})"
    if schema_type == "object":
        properties = schema.get("properties")
        if not isinstance(properties, dict):
            additional = schema.get("additionalProperties")
            if isinstance(additional, dict):
                rendered_value = render_schema(
                    additional, indent, require_all, allow_helpers)
                return f"Map({rendered_value})"
            return "JsonMap()"
        if not properties:
            return "Object()"
        lines = ["Object(new[]", indent + "{"]
        for name, child in properties.items():
            lines.append(
                indent + "    " + f'Field("{name}", ' +
                render_schema(child, indent + "    ", require_all, allow_helpers) + "),")
        closing = indent + "}"
        required_names = list(properties) if require_all else list(schema.get("required", []))
        if required_names:
            required = ", ".join(f'"{name}"' for name in required_names)
            closing += ", " + required
        lines.append(closing + ")")
        return "\n".join(lines)
    if isinstance(schema_type, str):
        return f'Type("{schema_type}")'
    return "JsonValue()"


def write_generated(report: dict[str, object]) -> pathlib.Path:
    output = EDITOR / "VmAutomationGeneratedRouteContracts.cs"
    routes = report["routes"]
    generated_inputs = [route for route in routes if route["generatedInput"] is not None]
    generated_outputs = [route for route in routes if route["outputShapes"]]
    lines = [
        "// <auto-generated />",
        "using System.Collections.Generic;",
        "using static VMUnityAutomation.Editor.VmAutomationRouteSchemaFactory;",
        "",
        "namespace VMUnityAutomation.Editor",
        "{",
        "    internal static class VmAutomationGeneratedRouteContracts",
        "    {",
        "        internal static bool TryGetInput(string route, out Dictionary<string, object> schema)",
        "        {",
        "            switch (route)",
        "            {",
    ]
    for route in generated_inputs:
        identifier = csharp_identifier("Input", route["route"])
        lines.extend([
            f'                case "{route["route"]}":',
            f"                    schema = {identifier}();",
            "                    return true;",
        ])
    lines.extend([
        "                default:",
        "                    schema = null;",
        "                    return false;",
        "            }",
        "        }",
        "",
        "        internal static bool TryGetOutput(string route, out Dictionary<string, object> schema)",
        "        {",
        "            switch (route)",
        "            {",
    ])
    for route in generated_outputs:
        identifier = csharp_identifier("Output", route["route"])
        lines.extend([
            f'                case "{route["route"]}":',
            f"                    schema = {identifier}();",
            "                    return true;",
        ])
    lines.extend([
        "                default:",
        "                    schema = null;",
        "                    return false;",
        "            }",
        "        }",
        "",
    ])
    for route in generated_inputs:
        identifier = csharp_identifier("Input", route["route"])
        rendered = render_schema(route["generatedInput"], "            ", False)
        lines.extend([
            f"        private static Dictionary<string, object> {identifier}()",
            "        {",
            "            return Root(" + rendered.replace("\n", "\n            ") + ");",
            "        }",
            "",
        ])
    for helper_name, helper_schema in REUSABLE_OUTPUT_SCHEMAS.items():
        rendered = render_schema(
            helper_schema, "            ", False, allow_helpers=False)
        lines.extend([
            f"        private static Dictionary<string, object> {helper_name}()",
            "        {",
            "            return " + rendered.replace("\n", "\n            ") + ";",
            "        }",
            "",
        ])
    for route in generated_outputs:
        identifier = csharp_identifier("Output", route["route"])
        shapes = route["outputShapes"]
        if len(shapes) == 1:
            rendered = render_schema(shapes[0], "            ", False)
        else:
            variants = [render_schema(shape, "                ", False) for shape in shapes]
            rendered = "OneOf(\n                " + ",\n                ".join(variants) + ")"
        lines.extend([
            f"        private static Dictionary<string, object> {identifier}()",
            "        {",
            "            return Root(" + rendered.replace("\n", "\n            ") + ");",
            "        }",
            "",
        ])
    lines.extend(["    }", "}", ""])
    output.write_text("\n".join(lines), encoding="utf-8", newline="\n")
    return output


def route_manifest_sha256(routes: Iterable[str]) -> str:
    canonical = "\n".join(sorted(
        route.strip("/") for route in routes if route.strip("/")))
    return hashlib.sha256(canonical.encode("utf-8")).hexdigest()


def write_route_manifest_audit_fingerprints(
        handlers: dict[str, str]) -> pathlib.Path:
    source = TOOL_CONFIGURATION_POLICY.read_text(encoding="utf-8")
    replacements = {
        "AuditedCoreRouteManifestSha256": route_manifest_sha256(
            route for route in handlers if not route.startswith("localization/")),
        "AuditedLocalizationRouteManifestSha256": route_manifest_sha256(
            route for route in handlers if route.startswith("localization/")),
    }
    for constant_name, fingerprint in replacements.items():
        pattern = re.compile(
            rf'(internal const string {constant_name}\s*=\s*)"[0-9a-f]{{64}}"')
        source, replacement_count = pattern.subn(
            rf'\1"{fingerprint}"', source)
        if replacement_count != 1:
            raise RuntimeError(
                f"Expected exactly one {constant_name} declaration, found "
                f"{replacement_count}.")
    TOOL_CONFIGURATION_POLICY.write_text(
        source, encoding="utf-8", newline="\n")
    return TOOL_CONFIGURATION_POLICY


def write_descriptor_registry() -> tuple[pathlib.Path, pathlib.Path, pathlib.Path]:
    handlers = parse_dispatch_handlers()
    deferred_routes = parse_deferred_routes()
    lines = [
        "// <auto-generated />",
        "using System;",
        "using System.Collections.Generic;",
        "using System.Linq;",
        "using static VMUnityAutomation.Editor.VmAutomationBuiltInRouteDescriptor;",
        "",
        "namespace VMUnityAutomation.Editor",
        "{",
        "    internal static class VmAutomationBuiltInRouteDescriptorRegistry",
        "    {",
        "        private static readonly VmAutomationBuiltInRouteDescriptor[] CoreDescriptorArray =",
        "        {",
    ]
    for route in sorted(route for route in handlers
                        if not route.startswith("localization/")):
        handler = handlers[route]
        if route in deferred_routes:
            registration = f'CreateDeferred("{route}", {handler})'
        else:
            registration = f'CreateImmediate("{route}", arguments => {handler})'
        rendered = registration.replace("\n", "\n                ")
        lines.append("            " + rendered + ",")
    lines.extend([
        "        };",
        "",
        "        private static readonly VmAutomationBuiltInRouteDescriptor[] DescriptorArray =",
        "            VmAutomationBuiltInRouteProviderCatalog.Merge(CoreDescriptorArray,",
        "                VmAutomationToolConfigurationPolicy.AuditedCoreRouteManifestSha256);",
        "",
        "        private static readonly IReadOnlyDictionary<string, VmAutomationBuiltInRouteDescriptor> ByRoute =",
        "            BuildByRoute();",
        "",
        "        internal static IReadOnlyList<VmAutomationBuiltInRouteDescriptor> Descriptors => DescriptorArray;",
        "",
        "        internal static IEnumerable<string> CoreRoutes => CoreDescriptorArray.Select(item => item.Route);",
        "",
        "        internal static IEnumerable<string> Routes => DescriptorArray.Select(item => item.Route);",
        "",
        "        internal static IEnumerable<string> ImmediateRoutes =>",
        "            DescriptorArray.Where(item => !item.IsDeferred).Select(item => item.Route);",
        "",
        "        internal static IEnumerable<string> DeferredRoutes =>",
        "            DescriptorArray.Where(item => item.IsDeferred).Select(item => item.Route);",
        "",
        "        internal static bool Contains(string route)",
        "        {",
        "            return !string.IsNullOrWhiteSpace(route) &&",
        "                   ByRoute.ContainsKey(route.Trim('/'));",
        "        }",
        "",
        "        internal static bool TryGet(string route, out VmAutomationBuiltInRouteDescriptor descriptor)",
        "        {",
        "            descriptor = null;",
        "            return !string.IsNullOrWhiteSpace(route) &&",
        "                   ByRoute.TryGetValue(route.Trim('/'), out descriptor);",
        "        }",
        "",
        "        internal static bool TryGetDeferred(string route,",
        "            out VmAutomationBuiltInRouteDescriptor.DeferredHandler handler)",
        "        {",
        "            handler = null;",
        "            if (!TryGet(route, out VmAutomationBuiltInRouteDescriptor descriptor) ||",
        "                !descriptor.IsDeferred)",
        "                return false;",
        "            handler = descriptor.Deferred;",
        "            return true;",
        "        }",
        "",
        "        internal static object Dispatch(string route, Dictionary<string, object> arguments)",
        "        {",
        "            if (!TryGet(route, out VmAutomationBuiltInRouteDescriptor descriptor))",
        "                return VmAutomationResponse.Error($\"Unknown automation route '{route}'.\", \"unknown_route\");",
        "            if (descriptor.IsDeferred)",
        "                return VmAutomationResponse.Error(",
        "                    $\"Route '{route}' requires deferred automation execution.\",",
        "                    \"deferred_route_required\");",
        "            return descriptor.Immediate(arguments ?? new Dictionary<string, object>());",
        "        }",
        "",
        "        private static IReadOnlyDictionary<string, VmAutomationBuiltInRouteDescriptor> BuildByRoute()",
        "        {",
        "            var result = new Dictionary<string, VmAutomationBuiltInRouteDescriptor>(StringComparer.Ordinal);",
        "            foreach (VmAutomationBuiltInRouteDescriptor descriptor in DescriptorArray)",
        "            {",
        "                if (result.ContainsKey(descriptor.Route))",
        "                    throw new InvalidOperationException(",
        "                        $\"Duplicate built-in route descriptor '{descriptor.Route}'.\");",
        "                result.Add(descriptor.Route, descriptor);",
        "            }",
        "            return result;",
        "        }",
        "",
        "    }",
        "}",
        "",
    ])
    DESCRIPTOR_REGISTRY.write_text("\n".join(lines), encoding="utf-8", newline="\n")

    localization_lines = [
        "// <auto-generated />",
        "using System.Collections.Generic;",
        "using static VMUnityAutomation.Editor.VmAutomationBuiltInRouteDescriptor;",
        "",
        "[assembly: VMUnityAutomation.Editor.VmAutomationBuiltInRouteProvider(",
        "    typeof(VMUnityAutomation.Editor.Localization.VmAutomationLocalizationRouteProvider))]",
        "",
        "namespace VMUnityAutomation.Editor.Localization",
        "{",
        "    internal sealed class VmAutomationLocalizationRouteProvider : IVmAutomationBuiltInRouteProvider",
        "    {",
        "        private static readonly VmAutomationBuiltInRouteDescriptor[] DescriptorArray =",
        "        {",
    ]
    for route in sorted(route for route in handlers
                        if route.startswith("localization/")):
        handler = handlers[route]
        if route in deferred_routes:
            registration = f'CreateDeferred("{route}", {handler})'
        else:
            registration = f'CreateImmediate("{route}", arguments => {handler})'
        rendered = registration.replace("\n", "\n                ")
        localization_lines.append("            " + rendered + ",")
    localization_lines.extend([
        "        };",
        "",
        "        public IReadOnlyList<VmAutomationBuiltInRouteDescriptor> Descriptors => DescriptorArray;",
        "",
        "        public string AuditedRouteManifestSha256 =>",
        "            VmAutomationToolConfigurationPolicy.AuditedLocalizationRouteManifestSha256;",
        "    }",
        "}",
        "",
    ])
    LOCALIZATION_DESCRIPTOR_PROVIDER.write_text(
        "\n".join(localization_lines), encoding="utf-8", newline="\n")
    configuration_policy = write_route_manifest_audit_fingerprints(handlers)
    return DESCRIPTOR_REGISTRY, LOCALIZATION_DESCRIPTOR_PROVIDER, configuration_policy


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--summary", action="store_true")
    parser.add_argument("--route")
    parser.add_argument("--source")
    parser.add_argument("--method")
    parser.add_argument("--write", action="store_true")
    arguments = parser.parse_args()
    if arguments.method:
        methods = index_methods()
        class_name, separator, method_name = arguments.method.partition(".")
        candidates = methods.get((class_name, method_name), []) if separator else []
        if not candidates:
            return 1
        method = max(candidates, key=lambda candidate: len(candidate.body))
        print(f"{method.class_name}.{method.name}({method.parameters})")
        print(method.body)
        return 0
    if arguments.source:
        methods = index_methods()
        handler = parse_dispatch_handlers().get(arguments.source, "")
        method = method_for_handler(handler, methods)
        print(handler)
        if method:
            print(f"{method.class_name}.{method.name}({method.parameters})")
            print(method.body)
            return 0
        return 1
    report = audit()
    if arguments.write:
        output = write_generated(report)
        print(output)
        registry, localization_provider, configuration_policy = write_descriptor_registry()
        print(registry)
        print(localization_provider)
        print(configuration_policy)
        return 0
    if arguments.route:
        matches = [item for item in report["routes"] if item["route"] == arguments.route]
        print(json.dumps(matches[0] if matches else {}, indent=2, ensure_ascii=False))
        return 0 if matches else 1
    if arguments.summary:
        print(json.dumps({key: value for key, value in report.items() if key != "routes"},
                         indent=2, ensure_ascii=False))
        unresolved = [item["route"] for item in report["routes"]
                      if item["route"] not in NON_CATALOG_ROUTES and
                      (not item["outputShapes"] or item["unresolvedOutputFields"])]
        print("routesNeedingReview=" + str(len(unresolved)))
        for route in unresolved:
            print(route)
        return 0
    print(json.dumps(report, indent=2, ensure_ascii=False))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
