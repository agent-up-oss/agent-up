'use strict';

// CommonJS copy of decode-uri-component@0.5.0 (GHSA-vcc3-ghjq-m6fr).
// query-string@7 does require('decode-uri-component'), so the published
// ESM 0.5.0 build cannot be used as an npm override under Metro.

const token = '%[a-f0-9]{2}';
const multiMatcher = new RegExp(`(${token})+`, 'gi');

const hexPair = /^[a-f\d]{2}$/i;

function parsePercentByte(input, position) {
  if (input.codePointAt(position) !== 37 || position + 3 > input.length) {
    return;
  }

  const digits = input.slice(position + 1, position + 3);

  if (!hexPair.test(digits)) {
    return;
  }

  return { byte: Number.parseInt(digits, 16), next: position + 3 };
}

function utf8SequenceLength(byte) {
  if (byte <= 0x7F) {
    return 1;
  }

  if (byte >= 0xC2 && byte <= 0xDF) {
    return 2;
  }

  if (byte >= 0xE0 && byte <= 0xEF) {
    return 3;
  }

  if (byte >= 0xF0 && byte <= 0xF4) {
    return 4;
  }

  return 0;
}

function isContinuationByte(byte) {
  return byte >= 0x80 && byte <= 0xBF;
}

function decode(input) {
  try {
    return decodeURIComponent(input);
  } catch {
    let output = '';
    let position = 0;

    while (position < input.length) {
      if (input.codePointAt(position) !== 37) {
        output += input.charAt(position);
        position++;
        continue;
      }

      const firstByte = parsePercentByte(input, position);

      if (!firstByte) {
        output += input.charAt(position);
        position++;
        continue;
      }

      const sequenceLength = utf8SequenceLength(firstByte.byte);

      if (sequenceLength === 0) {
        output += input.slice(position, position + 3);
        position += 3;
        continue;
      }

      let end = firstByte.next;
      let validSequence = true;

      for (let index = 1; index < sequenceLength; index++) {
        const nextByte = parsePercentByte(input, end);

        if (!nextByte || !isContinuationByte(nextByte.byte)) {
          validSequence = false;
          break;
        }

        end = nextByte.next;
      }

      if (validSequence) {
        const encodedSequence = input.slice(position, end);

        try {
          output += decodeURIComponent(encodedSequence);
          position = end;
          continue;
        } catch {
          // Invalid UTF-8 despite correct structure — emit the first byte literally.
        }
      }

      output += input.slice(position, position + 3);
      position += 3;
    }

    return output;
  }
}

function customDecodeURIComponent(input) {
  const replaceMap = {
    '%FE%FF': '\uFFFD\uFFFD',
    '%FF%FE': '\uFFFD\uFFFD',
  };

  let match = multiMatcher.exec(input);

  while (match) {
    try {
      replaceMap[match[0]] = decodeURIComponent(match[0]);
    } catch {
      const result = decode(match[0]);

      if (result !== match[0]) {
        replaceMap[match[0]] = result;
      }
    }

    match = multiMatcher.exec(input);
  }

  replaceMap['%C2'] = '\uFFFD';

  const entries = Object.keys(replaceMap);

  for (const key of entries) {
    input = input.replace(new RegExp(key, 'g'), replaceMap[key]);
  }

  return input;
}

module.exports = function decodeUriComponent(encodedURI) {
  if (typeof encodedURI !== 'string') {
    throw new TypeError(`Expected \`encodedURI\` to be of type \`string\`, got \`${typeof encodedURI}\``);
  }

  try {
    return decodeURIComponent(encodedURI);
  } catch {
    return customDecodeURIComponent(encodedURI);
  }
};
