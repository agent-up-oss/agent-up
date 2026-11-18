'use strict';

const { Transform } = require('node:stream');
const { StringDecoder } = require('node:string_decoder');

class JsonlParser extends Transform {
  static make(options) {
    return new JsonlParser(options);
  }

  constructor(options = {}) {
    super({ readableObjectMode: true, writableObjectMode: false });
    this._decoder = new StringDecoder('utf8');
    this._rest = '';
    this._counter = 0;
    this._reviver = options.reviver;
    this._checkErrors = Boolean(options.checkErrors);
  }

  _transform(chunk, encoding, callback) {
    this._consume(this._decoder.write(chunk), callback);
  }

  _flush(callback) {
    const trailing = this._rest + this._decoder.end();
    this._rest = '';
    if (!trailing) {
      callback(null);
      return;
    }
    this._emitLine(trailing, callback);
  }

  _consume(text, callback) {
    const lines = (this._rest + text).split('\n');
    this._rest = lines.pop();
    for (const line of lines) {
      if (!line) {
        continue;
      }
      const error = this._emitLine(line);
      if (error) {
        callback(error);
        return;
      }
    }
    callback(null);
  }

  _emitLine(line, callback) {
    try {
      this.push({ key: this._counter++, value: JSON.parse(line, this._reviver) });
    } catch (error) {
      if (this._checkErrors) {
        if (callback) {
          callback(error);
        }
        return error;
      }
      throw error;
    }
    if (callback) {
      callback(null);
    }
    return null;
  }
}

JsonlParser.parser = JsonlParser.make;
JsonlParser.make.Constructor = JsonlParser;

module.exports = JsonlParser;
