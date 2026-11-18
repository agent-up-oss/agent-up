'use strict';

const { Transform } = require('node:stream');
const { StringDecoder } = require('node:string_decoder');

class StreamArray extends Transform {
  static make() {
    return new StreamArray();
  }

  static withParser() {
    return new StreamArray();
  }

  constructor() {
    super({ writableObjectMode: false, readableObjectMode: true });
    this._decoder = new StringDecoder('utf8');
    this._buffer = '';
  }

  _transform(chunk, encoding, callback) {
    this._buffer += typeof chunk === 'string' ? chunk : this._decoder.write(chunk);
    callback(null);
  }

  _flush(callback) {
    const text = this._buffer + this._decoder.end();
    let parsed;
    try {
      parsed = JSON.parse(text);
    } catch (error) {
      callback(error);
      return;
    }

    if (!Array.isArray(parsed)) {
      callback(new Error('Top-level object should be an array.'));
      return;
    }

    for (let key = 0; key < parsed.length; key += 1) {
      this.push({ key, value: parsed[key] });
    }
    callback(null);
  }
}

StreamArray.streamArray = StreamArray.make;
StreamArray.make.Constructor = StreamArray;

module.exports = StreamArray;
