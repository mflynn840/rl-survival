import { GLTFLoader } from './vendor/loaders/GLTFLoader.js';

const loader = new GLTFLoader();
const cache = new Map();

/** Clone an editable Blender model exported as glTF. Every actor gets its own joints. */
export async function model(name) {
  if (!cache.has(name)) cache.set(name, loader.loadAsync(`/models/${name}.glb`).then(result => result.scene));
  const original = await cache.get(name);
  const copy = original.clone(true);
  copy.traverse(node => {
    if (node.isMesh) {
      node.castShadow = true;
      node.receiveShadow = true;
      node.material = node.material.clone();
    }
  });
  return copy;
}

export const gunNames = ['Carbine', 'Shotgun', 'Railgun', 'SMG', 'GrenadeLauncher', 'LMG'];
export const blockNames = ['Scrap', 'Brick', 'Steel', 'Titanium', 'ShockGrid', 'ReinforcedConcrete', 'Roof', 'Spikes'];
