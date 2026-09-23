import definition from '../../../../../AgentUp.FakeServer/definition.json';
import { cloneDefinition, type FakeServerDefinition } from '../models/FakeServerDefinition';
import { fakeServerId, isFakeServerUrl } from '../models/FakeServerIdentity';

export function loadFakeServerDefinition(): FakeServerDefinition {
  const loaded = cloneDefinition(definition as FakeServerDefinition);
  if (loaded.id !== fakeServerId)
    throw new Error('The fake server definition id does not match the client catalog.');
  if (!isFakeServerUrl(loaded.url))
    throw new Error('The fake server definition URL does not match the client catalog.');
  return loaded;
}
