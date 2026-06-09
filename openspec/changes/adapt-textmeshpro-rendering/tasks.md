## 1. Branch and Specification

- [x] 1.1 Create `feature/textmeshpro-adapter` from `master`
- [x] 1.2 Push `feature/textmeshpro-adapter` to `origin` and set upstream
- [x] 1.3 Create OpenSpec change `adapt-textmeshpro-rendering`
- [x] 1.4 Author proposal, design, requirements, and task list
- [x] 1.5 Validate the OpenSpec change

## 2. TextMeshPro Import

- [x] 2.1 Add `com.unity.textmeshpro` to `Packages/manifest.json`
- [x] 2.2 Resolve Unity packages and update `Packages/packages-lock.json`
- [x] 2.3 Import TMP Essential Resources into `Assets/TextMesh Pro`
- [ ] 2.4 Add required asmdef references for TMP-dependent code

## 3. Low-Level TMP Adapter

- [ ] 3.1 Add a TMP draw target that implements `IUIDrawTarget`
- [ ] 3.2 Extract supported TMP mesh data into the existing `IUIData` shape
- [ ] 3.3 Bind the TMP font atlas for batch rendering
- [ ] 3.4 Update text mutation APIs to support both legacy Text and TMP text targets
- [ ] 3.5 Add EditMode tests for TMP mesh generation, text mutation, and transform handling
- [ ] 3.6 Add a PlayMode or sample-scene validation for TMP batch rendering

## 4. Advanced TMP Rendering

- [ ] 4.1 Add or extend shader support for TMP SDF sampling
- [ ] 4.2 Define supported TMP outline behavior without relying on `UIOutline`
- [ ] 4.3 Document unsupported advanced cases such as fallback atlas, TMP sprites, and multi-material text
- [ ] 4.4 Add tests or scene validation for SDF and outline behavior

## 5. Regression and Delivery

- [ ] 5.1 Run legacy Text validation to confirm no regression
- [ ] 5.2 Run TMP-specific tests and record artifacts
- [ ] 5.3 Commit and push the branch updates
